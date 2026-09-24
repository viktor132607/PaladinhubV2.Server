using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services;
using PaladinHubV2.Server.Domain.Services.Checkout;
using CheckoutPaymentMethod =
	PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[AllowAnonymous]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICheckoutSessionService _checkoutSession;
		private readonly ICheckoutOrderService _checkoutOrders;
		private readonly ICartStore? _cartStore;
		private readonly string _clientBaseUrl;

		public CheckoutController(
			UserManager<User> userManager,
			ICheckoutSessionService checkoutSession,
			ICheckoutOrderService checkoutOrders,
			IConfiguration configuration,
			ICartStore? cartStore = null)
		{
			_userManager = userManager;
			_checkoutSession = checkoutSession;
			_checkoutOrders = checkoutOrders;
			_cartStore = cartStore;

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
			CheckoutState state = _checkoutSession.GetState();
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

			_checkoutSession.NormalizeShipping(model);
			CheckoutState state = _checkoutSession.GetState();
			state.Shipping = model;
			_checkoutSession.SaveState(state);

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
			CheckoutState state = _checkoutSession.GetState();

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

			CheckoutState state = _checkoutSession.GetState();

			if (state.Shipping == null)
			{
				return Conflict(new
				{
					message = "Shipping details are required.",
					redirect = "/Checkout/Shipping"
				});
			}

			if (User.Identity?.IsAuthenticated != true &&
				model.Method == CheckoutPaymentMethod.Balance)
			{
				return BadRequest(new
				{
					message = "Balance payment requires a signed-in account."
				});
			}

			state.PaymentMethod = model.Method;
			_checkoutSession.SaveState(state);

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

			if (user == null && _cartStore == null)
			{
				return Unauthorized(new { message = "Authentication required." });
			}

			CheckoutState state = _checkoutSession.GetState();

			if (state?.Shipping == null || state.PaymentMethod == null)
			{
				return Conflict(new
				{
					message = "Shipping details or payment method are missing.",
					redirect = "/Checkout/Shipping"
				});
			}

			if (user == null)
			{
				try
				{
					user = await CheckoutGuestUserResolver.ResolveOrCreateAsync(
						HttpContext,
						User,
						_userManager,
						_cartStore!,
						_checkoutSession,
						cancellationToken);
				}
				catch (InvalidOperationException error)
				{
					return BadRequest(new
					{
						message = error.Message,
						redirect = "/Checkout/Shipping"
					});
				}
			}

			CheckoutCartSnapshot snapshot =
				await _checkoutOrders.GetCartSnapshotAsync(user, cancellationToken);

			state.Total = snapshot.Total;
			_checkoutSession.SaveState(state);

			if (state.Total <= 0m || snapshot.Items <= 0)
			{
				return BadRequest(new
				{
					message = "Your cart is empty.",
					redirect = "/Cart/MyCart"
				});
			}

			CheckoutPaymentReview paymentReview =
				await _checkoutOrders.GetPaymentReviewAsync(
					user,
					state,
					state.Total);
			_checkoutSession.SaveState(state);

			return Ok(new
			{
				shipping = state.Shipping,
				paymentMethod = state.PaymentMethod,
				total = state.Total,
				items = snapshot.Items,
				walletBalance = paymentReview.WalletBalance,
				walletUsdPerEur = state.PaymentMethod == PaladinHub.Models.Checkout.PaymentMethod.Balance ? state.UsdPerEur : 0m,
				paymentError = paymentReview.PaymentError,
				orderId = state.OrderId,
				isGuest = User.Identity?.IsAuthenticated != true
			});
		}
	}
}
