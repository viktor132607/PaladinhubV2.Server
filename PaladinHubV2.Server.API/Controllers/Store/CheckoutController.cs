using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Checkout;
using CheckoutPaymentMethod = PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICheckoutStateService _state;
		private readonly ICheckoutService _checkout;
		private readonly string _clientBaseUrl;

		public CheckoutController(
			UserManager<User> userManager,
			ICheckoutStateService state,
			ICheckoutService checkout,
			IConfiguration configuration)
		{
			_userManager = userManager;
			_state = state;
			_checkout = checkout;
			_clientBaseUrl =
				(configuration["ClientApp:BaseUrl"] ?? "http://localhost:3000")
				.TrimEnd('/');
		}

		[HttpGet("Start")]
		public IActionResult Start()
		{
			const string redirectPath = "/Checkout/Shipping";

			bool acceptsJson = Request.Headers.Accept.ToString().Contains(
				"application/json",
				StringComparison.OrdinalIgnoreCase);

			return acceptsJson
				? Ok(new { redirect = redirectPath })
				: Redirect($"{_clientBaseUrl}{redirectPath}");
		}

		[HttpGet("Shipping")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public IActionResult Shipping()
		{
			CheckoutState state = _state.Get();
			return Ok(state.Shipping ?? new ShippingInfoVM());
		}

		[HttpPost("Shipping")]
		[ValidateAntiForgeryToken]
		public IActionResult Shipping([FromBody] ShippingInfoVM model)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			_state.NormalizeShipping(model);

			CheckoutState state = _state.Get();
			state.Shipping = model;
			_state.Save(state);

			return Ok(new
			{
				ok = true,
				shipping = state.Shipping,
				redirect = "/Checkout/Payment"
			});
		}

		[HttpGet("Payment")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public IActionResult Payment()
		{
			CheckoutState state = _state.Get();

			if (state.Shipping == null)
			{
				return Conflict(new
				{
					message = "Shipping details are required.",
					redirect = "/Checkout/Shipping"
				});
			}

			return Ok(new PaymentVM
			{
				Method = state.PaymentMethod ?? CheckoutPaymentMethod.Card
			});
		}

		[HttpPost("Payment")]
		[ValidateAntiForgeryToken]
		public IActionResult Payment([FromBody] PaymentVM model)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			if (!Enum.IsDefined(typeof(CheckoutPaymentMethod), model.Method))
			{
				return BadRequest(new { message = "Invalid payment method." });
			}

			CheckoutState state = _state.Get();

			if (state.Shipping == null)
			{
				return Conflict(new
				{
					message = "Shipping details are required.",
					redirect = "/Checkout/Shipping"
				});
			}

			state.PaymentMethod = model.Method;
			_state.Save(state);

			return Ok(new
			{
				ok = true,
				paymentMethod = state.PaymentMethod,
				redirect = "/Checkout/Review"
			});
		}

		[HttpGet("Review")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Review(
			CancellationToken cancellationToken)
		{
			User? user = await _userManager.GetUserAsync(User);

			if (user == null)
			{
				return Unauthorized(new { message = "Authentication required." });
			}

			CheckoutState state = _state.Get();

			CheckoutOperationResult<CheckoutReviewData> result =
				await _checkout.ReviewAsync(user, state, cancellationToken);

			_state.Save(state);

			if (!result.Succeeded)
			{
				return result.Code == CheckoutResultCode.MissingCheckoutDetails
					? Conflict(new { message = result.Message, redirect = result.Redirect })
					: BadRequest(new { message = result.Message, redirect = result.Redirect });
			}

			return Ok(result.Value);
		}
	}
}
