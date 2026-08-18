using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Checkout;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutCardController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICheckoutStateService _state;
		private readonly ICheckoutService _checkout;

		public CheckoutCardController(
			UserManager<User> userManager,
			ICheckoutStateService state,
			ICheckoutService checkout)
		{
			_userManager = userManager;
			_state = state;
			_checkout = checkout;
		}

		[HttpGet("Card")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Card(
			CancellationToken cancellationToken)
		{
			User? user = await _userManager.GetUserAsync(User);

			if (user == null)
			{
				return Unauthorized(new { message = "Authentication required." });
			}

			CheckoutState state = _state.Get();

			CheckoutOperationResult<CheckoutCardSessionData> result =
				await _checkout.CreateCardSessionAsync(
					user,
					state,
					cancellationToken);

			_state.Save(state);

			if (!result.Succeeded)
			{
				return result.Code switch
				{
					CheckoutResultCode.MissingShipping or
					CheckoutResultCode.CardNotSelected => Conflict(new
					{
						message = result.Message,
						redirect = result.Redirect
					}),
					CheckoutResultCode.StripeNotConfigured => StatusCode(
						StatusCodes.Status503ServiceUnavailable,
						new { message = result.Message }),
					CheckoutResultCode.StripeCreateFailed => StatusCode(
						StatusCodes.Status502BadGateway,
						new { message = result.Message }),
					_ => BadRequest(new
					{
						message = result.Message,
						redirect = result.Redirect
					})
				};
			}

			return Ok(result.Value);
		}

		[HttpPost("Card/Finalize")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CardFinalize(
			[FromBody] CardFinalizeRequest request,
			CancellationToken cancellationToken)
		{
			if (request == null || string.IsNullOrWhiteSpace(request.PaymentIntentId))
			{
				return BadRequest(new { message = "Payment intent ID is required." });
			}

			User? user = await _userManager.GetUserAsync(User);

			if (user == null)
			{
				return Unauthorized(new { message = "Authentication required." });
			}

			CheckoutState state = _state.Get();

			CheckoutOperationResult<CheckoutFinalizeData> result =
				await _checkout.FinalizeCardAsync(
					user,
					state,
					request.PaymentIntentId,
					cancellationToken);

			_state.Save(state);

			if (!result.Succeeded)
			{
				return result.Code switch
				{
					CheckoutResultCode.StripeVerificationFailed => StatusCode(
						StatusCodes.Status502BadGateway,
						new { message = result.Message }),
					CheckoutResultCode.CartTotalChanged => Conflict(
						new { message = result.Message }),
					_ => BadRequest(new { message = result.Message })
				};
			}

			CheckoutFinalizeData value = result.Value!;

			if (value.ClearState)
			{
				_state.Clear();
			}

			return Ok(new
			{
				ok = true,
				orderId = value.OrderId,
				redirect = value.Redirect
			});
		}

		public sealed class CardFinalizeRequest
		{
			public string PaymentIntentId { get; init; } = string.Empty;
		}
	}
}
