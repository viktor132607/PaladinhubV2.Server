using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services;
using PaladinHubV2.Server.Domain.Services.Checkout;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[AllowAnonymous]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutCardsController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly CheckoutCardFlowService _cardFlow;
		private readonly ICartStore _cartStore;
		private readonly ICheckoutSessionService _checkoutSession;

		public CheckoutCardsController(
			UserManager<User> userManager,
			CheckoutCardFlowService cardFlow,
			ICartStore cartStore,
			ICheckoutSessionService checkoutSession)
		{
			_userManager = userManager;
			_cardFlow = cardFlow;
			_cartStore = cartStore;
			_checkoutSession = checkoutSession;
		}

		[HttpGet("Card")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Card(
			CancellationToken cancellationToken)
		{
			User user;
			try
			{
				user = await CheckoutGuestUserResolver.ResolveOrCreateAsync(
					HttpContext,
					User,
					_userManager,
					_cartStore,
					_checkoutSession,
					cancellationToken);
			}
			catch (InvalidOperationException error)
			{
				return Conflict(new
				{
					message = error.Message,
					redirect = "/Checkout/Shipping"
				});
			}

			CheckoutCardSetupResult result =
				await _cardFlow.PrepareAsync(
					user,
					cancellationToken);

			IActionResult? errorResult = MapError(result.Error);
			if (errorResult != null)
			{
				return errorResult;
			}

			return Ok(new
			{
				clientSecret = result.ClientSecret,
				publishableKey = result.PublishableKey,
				paymentIntentId = result.PaymentIntentId,
				orderId = result.OrderId,
				amount = result.Amount,
				currency = result.Currency
			});
		}

		private IActionResult? MapError(CheckoutCardFlowError error)
		{
			return error switch
			{
				CheckoutCardFlowError.None => null,
				CheckoutCardFlowError.ShippingRequired =>
					Conflict(new
					{
						message = "Shipping details are required.",
						redirect = "/Checkout/Shipping"
					}),
				CheckoutCardFlowError.CardNotSelected =>
					Conflict(new
					{
						message = "Card payment is not selected.",
						redirect = "/Checkout/Review"
					}),
				CheckoutCardFlowError.EmptyCart =>
					BadRequest(new
					{
						message = "Your cart is empty.",
						redirect = "/Cart/MyCart"
					}),
				CheckoutCardFlowError.StripeNotConfigured =>
					StatusCode(
						StatusCodes.Status503ServiceUnavailable,
						new { message = "Stripe is not configured." }),
				CheckoutCardFlowError.MissingClientSecret =>
					StatusCode(
						StatusCodes.Status502BadGateway,
						new
						{
							message = "Stripe did not return a client secret."
						}),
				CheckoutCardFlowError.SessionCreateFailed =>
					StatusCode(
						StatusCodes.Status502BadGateway,
						new
						{
							message = "Card payment session could not be created."
						}),
				_ =>
					StatusCode(
						StatusCodes.Status502BadGateway,
						new { message = "Card payment session could not be created." })
			};
		}
	}
}
