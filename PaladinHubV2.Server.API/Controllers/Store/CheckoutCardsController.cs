using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
	public sealed class CheckoutCardsController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICheckoutSessionService _checkoutSession;
		private readonly ICheckoutOrderService _checkoutOrders;
		private readonly ICheckoutCardPaymentService _cardPayments;

		public CheckoutCardsController(
			UserManager<User> userManager,
			ICheckoutSessionService checkoutSession,
			ICheckoutOrderService checkoutOrders,
			ICheckoutCardPaymentService cardPayments)
		{
			_userManager = userManager;
			_checkoutSession = checkoutSession;
			_checkoutOrders = checkoutOrders;
			_cardPayments = cardPayments;
		}

		[HttpGet("Card")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Card(
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

			if (state.Shipping == null)
			{
				return Conflict(new
				{
					message =
						"Shipping details are required.",

					redirect =
						"/Checkout/Shipping"
				});
			}

			if (state.PaymentMethod !=
				CheckoutPaymentMethod.Card)
			{
				return Conflict(new
				{
					message =
						"Card payment is not selected.",

					redirect =
						"/Checkout/Review"
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

			if (!_cardPayments.IsConfigured)
			{
				return StatusCode(
					StatusCodes.Status503ServiceUnavailable,
					new
					{
						message =
							"Stripe is not configured."
					});
			}

			state.Total = snapshot.Total;

			if (string.IsNullOrWhiteSpace(state.OrderId))
			{
				state.OrderId =
					Guid.NewGuid().ToString("N");
			}

			_checkoutSession.SaveState(state);

			CheckoutCardSessionResult paymentSession =
				await _cardPayments.CreateSessionAsync(
					user.Id,
					state.OrderId,
					state.Total,
					cancellationToken);

			if (paymentSession.Error ==
				CheckoutCardPaymentError.MissingClientSecret)
			{
				return StatusCode(
					StatusCodes.Status502BadGateway,
					new
					{
						message =
							"Stripe did not return a client secret."
					});
			}

			if (paymentSession.Error !=
				CheckoutCardPaymentError.None)
			{
				return StatusCode(
					StatusCodes.Status502BadGateway,
					new
					{
						message =
							"Card payment session could not be created."
					});
			}

			return Ok(new
			{
				clientSecret =
					paymentSession.ClientSecret,

				publishableKey =
					paymentSession.PublishableKey,

				paymentIntentId =
					paymentSession.PaymentIntentId,

				orderId =
					state.OrderId,

				amount =
					state.Total,

				currency =
					paymentSession.Currency
			});
		}

		[HttpPost("Card/Finalize")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CardFinalize(
			[FromBody] CardFinalizeRequest request,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(
					request.PaymentIntentId))
			{
				return BadRequest(new
				{
					message =
						"Payment intent ID is required."
				});
			}

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

			if (state.PaymentMethod !=
				CheckoutPaymentMethod.Card)
			{
				return BadRequest(new
				{
					message =
						"Card payment is not selected."
				});
			}

			if (string.IsNullOrWhiteSpace(state.OrderId))
			{
				return BadRequest(new
				{
					message =
						"Checkout order ID is missing."
				});
			}

			string orderId = state.OrderId;

			if (await _checkoutOrders
				.OrderTransactionExistsAsync(
					user.Id,
					orderId,
					cancellationToken))
			{
				await _checkoutOrders.ArchiveCartAsync(
					user,
					cancellationToken);

				_checkoutSession.Clear();

				return CheckoutSuccess(orderId);
			}

			CheckoutCardVerificationResult verification =
				await _cardPayments.VerifyAsync(
					request.PaymentIntentId,
					user.Id,
					orderId,
					cancellationToken);

			IActionResult? verificationError =
				MapVerificationError(verification.Error);

			if (verificationError != null)
			{
				return verificationError;
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
						"Your cart is empty."
				});
			}

			if (!_cardPayments.AmountMatches(
					snapshot.Total,
					verification.Amount))
			{
				return Conflict(new
				{
					message =
						"The cart total changed after the payment session was created."
				});
			}

			state.Total = snapshot.Total;

			_checkoutSession.SaveState(state);

			await _checkoutOrders.CompleteCardOrderAsync(
				user,
				state,
				cancellationToken);

			_checkoutSession.Clear();

			return CheckoutSuccess(orderId);
		}

		private IActionResult? MapVerificationError(
			CheckoutCardPaymentError error)
		{
			return error switch
			{
				CheckoutCardPaymentError.None =>
					null,

				CheckoutCardPaymentError.VerificationFailed =>
					StatusCode(
						StatusCodes.Status502BadGateway,
						new
						{
							message =
								"Stripe payment could not be verified."
						}),

				CheckoutCardPaymentError.PaymentNotCompleted =>
					BadRequest(new
					{
						message =
							"Payment was not completed."
					}),

				CheckoutCardPaymentError.CurrencyMismatch =>
					BadRequest(new
					{
						message =
							"Payment currency does not match the order."
					}),

				CheckoutCardPaymentError.OrderMismatch =>
					BadRequest(new
					{
						message =
							"Payment order does not match the checkout order."
					}),

				CheckoutCardPaymentError.UserMismatch =>
					BadRequest(new
					{
						message =
							"Payment user does not match the checkout user."
					}),

				_ =>
					StatusCode(
						StatusCodes.Status502BadGateway,
						new
						{
							message =
								"Stripe payment could not be verified."
						})
			};
		}

		private IActionResult CheckoutSuccess(
			string orderId)
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
			public string PaymentIntentId { get; init; } =
				string.Empty;
		}
	}
}
