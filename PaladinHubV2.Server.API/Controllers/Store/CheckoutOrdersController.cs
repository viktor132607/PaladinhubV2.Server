using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Checkout;
using CheckoutPaymentMethod =
	PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutOrdersController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICheckoutSessionService _checkoutSession;
		private readonly ICheckoutOrderService _checkoutOrders;

		public CheckoutOrdersController(
			UserManager<User> userManager,
			ICheckoutSessionService checkoutSession,
			ICheckoutOrderService checkoutOrders)
		{
			_userManager = userManager;
			_checkoutSession = checkoutSession;
			_checkoutOrders = checkoutOrders;
		}

		[HttpPost("PlaceOrder")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PlaceOrder(
			CancellationToken cancellationToken)
		{
			User? user =
				await _userManager.GetUserAsync(User);

			if (user == null)
			{
				return Unauthorized(new
				{
					message =
						"Authentication required."
				});
			}

			CheckoutState state =
				_checkoutSession.GetState();

			if (state.Shipping == null ||
				state.PaymentMethod == null)
			{
				return BadRequest(new
				{
					message =
						"Shipping details or payment method are missing.",

					redirect =
						"/Checkout/Shipping"
				});
			}

			CheckoutCartSnapshot snapshot =
				await _checkoutOrders.GetCartSnapshotAsync(
					user,
					cancellationToken);

			if (snapshot.Items <= 0 ||
				snapshot.Total <= 0m)
			{
				return BadRequest(new
				{
					message =
						"Your cart is empty.",

					redirect =
						"/Cart/MyCart"
				});
			}

			state.Total = snapshot.Total;

			if (string.IsNullOrWhiteSpace(state.OrderId))
			{
				state.OrderId =
					Guid.NewGuid().ToString("N");
			}

			_checkoutSession.SaveState(state);

			string orderId = state.OrderId;

			switch (state.PaymentMethod.Value)
			{
				case CheckoutPaymentMethod.CashOnDelivery:
				{
					CheckoutOrderPlacementResult result =
						await _checkoutOrders
							.PlaceCashOnDeliveryAsync(
								user,
								state,
								orderId,
								cancellationToken);

					_checkoutSession.Clear();

					return Ok(new
					{
						ok = result.Success,
						orderId,
						redirect = "/Checkout/Registered"
					});
				}

				case CheckoutPaymentMethod.Balance:
				{
					CheckoutOrderPlacementResult result =
						await _checkoutOrders.PlaceWalletAsync(
							user,
							state,
							orderId,
							cancellationToken);

					if (!result.Success)
					{
						return BadRequest(new
						{
							message =
								result.ErrorMessage ??
								"Insufficient wallet balance.",

							paymentError =
								result.ErrorMessage ??
								"Insufficient wallet balance.",

							redirect =
								"/Checkout/Review"
						});
					}

					_checkoutSession.Clear();

					return Ok(new
					{
						ok = true,
						orderId,
						redirect = "/Checkout/Success"
					});
				}

				case CheckoutPaymentMethod.Card:
					return Ok(new
					{
						ok = true,
						orderId,
						redirect = "/Checkout/Card"
					});

				default:
					return BadRequest(new
					{
						message =
							"Invalid payment method."
					});
			}
		}
	}
}
