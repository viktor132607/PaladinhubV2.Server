using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Checkout;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutCardFinalizeController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly CheckoutCardFlowService _cardFlow;

		public CheckoutCardFinalizeController(
			UserManager<User> userManager,
			CheckoutCardFlowService cardFlow)
		{
			_userManager = userManager;
			_cardFlow = cardFlow;
		}

		[HttpPost("Card/Finalize")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CardFinalize(
			[FromBody] CardFinalizeRequest request,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(request.PaymentIntentId))
			{
				return BadRequest(new
				{
					message = "Payment intent ID is required."
				});
			}

			User? user = await _userManager.GetUserAsync(User);
			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			CheckoutCardFinalizeResult result =
				await _cardFlow.FinalizeAsync(
					user,
					request.PaymentIntentId,
					cancellationToken);

			if (result.Error == CheckoutCardFlowError.PaymentVerificationFailed)
			{
				return MapVerificationError(result.PaymentError);
			}

			IActionResult? error = MapFlowError(result.Error);
			if (error != null)
			{
				return error;
			}

			return CheckoutSuccess(result.OrderId!);
		}

		private IActionResult? MapFlowError(CheckoutCardFlowError error)
		{
			return error switch
			{
				CheckoutCardFlowError.None => null,
				CheckoutCardFlowError.CardNotSelected =>
					BadRequest(new { message = "Card payment is not selected." }),
				CheckoutCardFlowError.MissingOrderId =>
					BadRequest(new { message = "Checkout order ID is missing." }),
				CheckoutCardFlowError.EmptyCart =>
					BadRequest(new { message = "Your cart is empty." }),
				CheckoutCardFlowError.AmountChanged =>
					Conflict(new
					{
						message = "The cart total changed after the payment session was created."
					}),
				_ =>
					StatusCode(
						StatusCodes.Status502BadGateway,
						new { message = "Stripe payment could not be verified." })
			};
		}

		private IActionResult MapVerificationError(
			CheckoutCardPaymentError error)
		{
			return error switch
			{
				CheckoutCardPaymentError.PaymentNotCompleted =>
					BadRequest(new { message = "Payment was not completed." }),
				CheckoutCardPaymentError.CurrencyMismatch =>
					BadRequest(new
					{
						message = "Payment currency does not match the order."
					}),
				CheckoutCardPaymentError.OrderMismatch =>
					BadRequest(new
					{
						message = "Payment order does not match the checkout order."
					}),
				CheckoutCardPaymentError.UserMismatch =>
					BadRequest(new
					{
						message = "Payment user does not match the checkout user."
					}),
				_ =>
					StatusCode(
						StatusCodes.Status502BadGateway,
						new { message = "Stripe payment could not be verified." })
			};
		}

		private IActionResult CheckoutSuccess(string orderId)
		{
			return Ok(new
			{
				ok = true,
				orderId,
				redirect =
					$"/Checkout/Success?orderId=" +
					Uri.EscapeDataString(orderId)
			});
		}

		public sealed class CardFinalizeRequest
		{
			public string PaymentIntentId { get; init; } = string.Empty;
		}
	}
}
