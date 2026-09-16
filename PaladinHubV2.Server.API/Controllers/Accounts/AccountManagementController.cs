using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController, Authorize, Route("api/account/manage")]
[EnableRateLimiting("account-security")]
public sealed class AccountManagementController(
    UserManager<User> users, SignInManager<User> signIn,
    AccountEmailService email, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(new { user.FullName, user.UserName, user.Email, user.EmailConfirmed,
            user.PhoneNumber, user.PhoneNumberConfirmed, user.AvatarPath,
            user.TwoFactorEnabled, emailTwoFactorEnabled = await AccountFactors.EmailAsync(users, user),
            authenticatorEnabled = await AccountFactors.AuthenticatorAsync(users, user) });
    }

    [HttpPost("profile"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(input.FullName)) return BadRequest(new { message = "Name is required." });
        user.FullName = input.FullName.Trim();
        string? phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        if (phone != user.PhoneNumber) user.PhoneNumberConfirmed = false;
        user.PhoneNumber = phone;
        var result = await users.UpdateAsync(user);
        return Result(result, "Profile saved.");
    }

    [HttpPost("send-verification"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SendVerification()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (user.EmailConfirmed) return Ok(new { message = "Email is already verified." });
        string token = await users.GenerateEmailConfirmationTokenAsync(user);
        await SendLink(user.Email!, "Verify your PaladinHub email", "/Account/VerifyEmail", user.Id, token);
        return Ok(new { message = "Verification email sent." });
    }

    [HttpPost("confirm-email"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEmail(TokenInput input)
    {
        var user = await users.FindByIdAsync(input.UserId);
        if (user is null) return BadRequest(new { message = "Invalid or expired verification link." });
        return Result(await users.ConfirmEmailAsync(user, input.Token), "Email verified.");
    }

    [HttpPost("email"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail(EmailChangeInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!await CheckPassword(user, input.Password)) return BadRequest(new { message = "Invalid password or account locked." });
        string address = input.Email.Trim();
        string token = await users.GenerateChangeEmailTokenAsync(user, address);
        await SendLink(address, "Confirm your new PaladinHub email", "/Account/VerifyEmail", user.Id, token, address);
        return Ok(new { message = "Confirm the link sent to your new email address." });
    }

    [HttpPost("confirm-email-change"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmChange(EmailChangeTokenInput input)
    {
        var user = await users.FindByIdAsync(input.UserId);
        if (user is null) return BadRequest(new { message = "Invalid or expired verification link." });
        var result = await users.ChangeEmailAsync(user, input.Email, input.Token);
        if (result.Succeeded) AccountFactors.Ensure(await users.UpdateSecurityStampAsync(user));
        return Result(result, "Email updated. Sign in again.");
    }

    [HttpGet("2fa-methods"), AllowAnonymous]
    public async Task<IActionResult> Methods()
    {
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return Unauthorized(new { message = "The two-factor login session has expired. Sign in again." });
        return Ok(new { authenticator = await AccountFactors.AuthenticatorAsync(users, user),
            email = await AccountFactors.EmailAsync(users, user) });
    }

    [HttpPost("send-login-code"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendLoginCode()
    {
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return Unauthorized(new { message = "The two-factor login session has expired. Sign in again." });
        if (!await AccountFactors.EmailAsync(users, user) || await users.IsLockedOutAsync(user)) return Forbid();
        HttpContext.Session.SetString(SessionEmailTokenProvider.NonceKey, Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        string code = await users.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
        await email.SendAsync(user.Email!, "Your PaladinHub login code", $"<p>Your login code is <strong>{WebUtility.HtmlEncode(code)}</strong>.</p><p>This code expires shortly. Do not share it.</p>");
        return Ok(new { message = "Login code sent. Check your inbox." });
    }

    [HttpPost("email-2fa"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailTwoFactor(FactorInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!await CheckPassword(user, input.Password)) return BadRequest(new { message = "Invalid password or account locked." });
        if (input.Enabled && !user.EmailConfirmed) return BadRequest(new { message = "Verify your email first." });
        bool authenticator = await AccountFactors.AuthenticatorAsync(users, user);
        AccountFactors.Ensure(await users.SetAuthenticationTokenAsync(user, AccountFactors.Store, "Authenticator2FA", authenticator ? "true" : "false"));
        AccountFactors.Ensure(await users.SetAuthenticationTokenAsync(user, AccountFactors.Store, "Email2FA", input.Enabled ? "true" : "false"));
        AccountFactors.Ensure(await users.SetTwoFactorEnabledAsync(user, input.Enabled || authenticator));
        AccountFactors.Ensure(await users.UpdateSecurityStampAsync(user));
        await signIn.RefreshSignInAsync(user);
        string[] recoveryCodes = [];
        if (input.Enabled && await users.CountRecoveryCodesAsync(user) == 0)
            recoveryCodes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray() ?? [];
        return Ok(new { message = input.Enabled ? "Email two-factor authentication enabled." : "Email two-factor authentication disabled.", recoveryCodes });
    }

    [HttpPost("forgot-password"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Forgot(EmailInput input)
    {
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is not null && user.EmailConfirmed)
            await SendLink(user.Email!, "Reset your PaladinHub password", "/Account/ResetPassword", user.Id, await users.GeneratePasswordResetTokenAsync(user));
        return Ok(new { message = "If an eligible account exists, a reset link has been sent." });
    }

    [HttpPost("reset-password"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(ResetInput input)
    {
        var user = await users.FindByIdAsync(input.UserId);
        if (user is null) return BadRequest(new { message = "Invalid or expired reset link." });
        return Result(await users.ResetPasswordAsync(user, input.Token, input.Password), "Password reset. Sign in with your new password.");
    }

    private async Task<bool> CheckPassword(User user, string password) =>
        (await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded;
    private IActionResult Result(IdentityResult result, string message) => result.Succeeded
        ? Ok(new { message }) : BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
    private Task SendLink(string address, string subject, string path, string userId, string token, string? newEmail = null)
    {
        string root = (configuration["ClientApp:BaseUrl"] ?? "").TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !uri.IsLoopback))
            throw new InvalidOperationException("ClientApp:BaseUrl must be configured for account emails.");
        string link = $"{root}{path}?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";
        if (newEmail is not null) link += $"&newEmail={Uri.EscapeDataString(newEmail)}";
        return email.SendAsync(address, subject, $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(subject)}</a></p><p>If you did not request this, ignore this email.</p>");
    }
}

public sealed class ProfileInput
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; init; } = "";
    [Phone, StringLength(32)] public string? PhoneNumber { get; init; }
}
public class EmailInput { [Required, EmailAddress] public string Email { get; init; } = ""; }
public sealed class EmailChangeInput : EmailInput { [Required] public string Password { get; init; } = ""; }
public class TokenInput
{
    [Required] public string UserId { get; init; } = "";
    [Required] public string Token { get; init; } = "";
}
public sealed class EmailChangeTokenInput : TokenInput { [Required, EmailAddress] public string Email { get; init; } = ""; }
public sealed class ResetInput : TokenInput
{
    [Required, StringLength(40, MinimumLength = 8)] public string Password { get; init; } = "";
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; init; } = "";
}
public sealed class FactorInput
{
    [Required] public string Password { get; init; } = "";
    public bool Enabled { get; init; }
}
