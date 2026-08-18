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
	public sealed class CheckoutOrdersController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICheckoutStateService _state;
		private readonly ICheckoutService _checkout;

		public CheckoutOrdersController(
			UserManager<User> userManager,
			ICheckoutStateService state,
			ICheckoutService checkout)
		{
			_userManager = userManager;
			_state = state;
			_checkout = checkout;
		}

		[HttpPost("PlaceOrder")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PlaceOrder(
			CancellationToken cancellationToken)
		{
			User? user = await _userManager.GetUserAsync(User);

			if (user == null)
			{
				return Unauthorized(new { message = "Authentication required." });
			}

			CheckoutState state = _state.Get();

			CheckoutOperationResult<CheckoutPlacementData> result =
				await _checkout.PlaceOrderAsync(user, state, cancellationToken);

			_state.Save(state);

			if (!result.Succeeded)
			{
				return result.Code switch
				{
					CheckoutResultCode.InsufficientWallet => BadRequest(new
					{
						message = result.Message,
						paymentError = result.Message,
						redirect = result.Redirect
					}),
					_ => BadRequest(new
					{
						message = result.Message,
						redirect = result.Redirect
					})
				};
			}

			CheckoutPlacementData value = result.Value!;

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

		[HttpGet("Registered")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public IActionResult Registered([FromQuery] string? orderId)
		{
			return Ok(new
			{
				orderId = orderId?.Trim() ?? string.Empty,
				status = "registered",
				message = "Your order was registered successfully."
			});
		}

		[HttpGet("Success")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public IActionResult Success([FromQuery] string? orderId)
		{
			return Ok(new
			{
				orderId = orderId?.Trim() ?? string.Empty,
				status = "success",
				message = "Payment completed successfully."
			});
		}

		[HttpGet("Failure")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public IActionResult Failure([FromQuery] string? message)
		{
			return Ok(new
			{
				status = "failure",
				message = string.IsNullOrWhiteSpace(message)
					? "Payment failed."
					: message.Trim()
			});
		}
	}
}
