using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using Stripe;
using Stripe.Checkout;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController, Authorize, Route("api/account/wallet")]
public sealed class WalletPaymentsController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("checkout"), ValidateAntiForgeryToken, EnableRateLimiting("account-security")]
    public async Task<IActionResult> Checkout(TopUpInput input)
    {
        if (decimal.Round(input.Amount, 2) != input.Amount) return BadRequest(new { message = "Use at most two decimal places." });
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        string root = (configuration["ClientApp:BaseUrl"] ?? "").TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !uri.IsLoopback))
            return StatusCode(503, new { message = "Payment return URL is not configured." });
        if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
            return StatusCode(503, new { message = "Card payments are not configured." });
        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "payment", ClientReferenceId = userId,
            SuccessUrl = root + "/Account/MyAccount?walletSession={CHECKOUT_SESSION_ID}",
            CancelUrl = root + "/Account/MyAccount?walletCancelled=1",
            Metadata = new Dictionary<string, string> { ["purpose"] = "wallet-top-up" },
            LineItems = [new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "usd", UnitAmount = (long)(input.Amount * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "PaladinHub wallet balance" }
                }
            }]
        });
        return Ok(new { url = session.Url });
    }

    [HttpPost("confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(CheckoutInput input)
    {
        var session = await new SessionService().GetAsync(input.SessionId);
        if (session.ClientReferenceId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return Forbid();
        if (!IsPaidWalletSession(session)) return Conflict(new { message = "Payment is not completed yet. Please check again shortly." });
        await Credit(session);
        return Ok(new { message = "Balance added successfully." });
    }

    [HttpPost("webhook"), AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        string? secret = configuration["Stripe:WalletWebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return StatusCode(503);
        string body = await new StreamReader(Request.Body).ReadToEndAsync();
        Event stripeEvent;
        try { stripeEvent = EventUtility.ConstructEvent(body, Request.Headers["Stripe-Signature"], secret); }
        catch (StripeException) { return BadRequest(); }
        if (stripeEvent.Type is "checkout.session.completed" or "checkout.session.async_payment_succeeded" &&
            stripeEvent.Data.Object is Session session && IsPaidWalletSession(session))
            await Credit(session);
        return Ok();
    }

    internal static bool IsPaidWalletSession(Session session) =>
        session.Mode == "payment" && session.PaymentStatus == "paid" && session.Currency == "usd" &&
        session.AmountTotal is >= 100 and <= 100000 && !string.IsNullOrWhiteSpace(session.ClientReferenceId) &&
        session.Metadata?.GetValueOrDefault("purpose") == "wallet-top-up";

    private async Task Credit(Session session)
    {
        // The same checkout always maps to the same primary key, including
        // concurrent webhook and browser confirmation requests.
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("wallet:" + session.Id)).AsSpan(0, 16));
        if (await db.Transactions.AnyAsync(t => t.Id == id)) return;
        var transaction = new Transaction
        {
            Id = id, UserId = session.ClientReferenceId, Amount = session.AmountTotal!.Value / 100m,
            Currency = "USD", Region = "US", CreatedAtUtc = DateTime.UtcNow,
            Status = TransactionStatus.Complete, Type = TransactionType.WalletTopUp,
            PurchaseTitle = "Wallet top-up", ExternalId = session.Id
        };
        db.Transactions.Add(transaction);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.Entry(transaction).State = EntityState.Detached;
            if (!await db.Transactions.AnyAsync(t => t.Id == id)) throw;
        }
    }
}
public sealed class TopUpInput { [Range(typeof(decimal), "1", "1000")] public decimal Amount { get; init; } }
public sealed class CheckoutInput { [Required, StringLength(255)] public string SessionId { get; init; } = ""; }
