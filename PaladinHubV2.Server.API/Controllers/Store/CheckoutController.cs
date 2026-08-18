using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaladinHub.Models.Checkout;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Wallet;
using Stripe;
using CheckoutPaymentMethod =
	PaladinHub.Models.Checkout.PaymentMethod;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutController : ControllerBase
	{
		private const string SessionKey = "checkout_state";
		private const string Currency = "USD";
		private const string Region = "US";

		private readonly UserManager<User> _userManager;
		private readonly ICartSessionService _cartSession;
		private readonly IProductService _productService;
		private readonly IWalletService _wallet;
		private readonly AppDbContext _db;
		private readonly string _stripePublishableKey;
		private readonly string _clientBaseUrl;

		public CheckoutController(
			UserManager<User> userManager,
			ICartSessionService cartSession,
			IProductService productService,
			IWalletService wallet,
			AppDbContext db,
			IConfiguration configuration)
		{
			_userManager = userManager;
			_cartSession = cartSession;
			_productService = productService;
			_wallet = wallet;
			_db = db;

			_stripePublishableKey =
				configuration["Stripe:PublishableKey"] ??
				string.Empty;

			_clientBaseUrl =
				(
					configuration["ClientApp:BaseUrl"] ??
					"http://localhost:3000"
				)
				.TrimEnd('/');
		}

		[HttpGet("Start")]
		public IActionResult Start()
		{
			const string redirectPath =
				"/Checkout/Shipping";

			bool acceptsJson =
				Request.Headers.Accept.ToString().Contains(
					"application/json",
					StringComparison.OrdinalIgnoreCase);

			if (acceptsJson)
			{
				return Ok(new
				{
					redirect = redirectPath
				});
			}

			return Redirect(
				$"{_clientBaseUrl}{redirectPath}");
		}

		[HttpGet("Shipping")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Shipping()
		{
			CheckoutState state = GetState();

			return Ok(
				state.Shipping ??
				new ShippingInfoVM());
		}

		[HttpPost("Shipping")]
		[ValidateAntiForgeryToken]
		public IActionResult Shipping(
			[FromBody] ShippingInfoVM model)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			NormalizeShipping(model);

			CheckoutState state = GetState();

			state.Shipping = model;

			SaveState(state);

			return Ok(new
			{
				ok = true,
				shipping = state.Shipping,
				redirect = "/Checkout/Payment"
			});
		}

		[HttpGet("Payment")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Payment()
		{
			CheckoutState state = GetState();

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

			return Ok(new PaymentVM
			{
				Method =
					state.PaymentMethod ??
					CheckoutPaymentMethod.Card
			});
		}

		[HttpPost("Payment")]
		[ValidateAntiForgeryToken]
		public IActionResult Payment(
			[FromBody] PaymentVM model)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			if (!Enum.IsDefined(
					typeof(CheckoutPaymentMethod),
					model.Method))
			{
				return BadRequest(new
				{
					message =
						"Invalid payment method."
				});
			}

			CheckoutState state = GetState();

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

			state.PaymentMethod = model.Method;

			SaveState(state);

			return Ok(new
			{
				ok = true,
				paymentMethod = state.PaymentMethod,
				redirect = "/Checkout/Review"
			});
		}

		[HttpGet("Review")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Review(
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();

			if (user == null)
			{
				return Unauthorized(new
				{
					message =
						"Authentication required."
				});
			}

			CheckoutState state = GetState();

			if (state.Shipping == null ||
				state.PaymentMethod == null)
			{
				return Conflict(new
				{
					message =
						"Shipping details or payment method are missing.",

					redirect =
						"/Checkout/Shipping"
				});
			}

			var snapshot = await GetCartSnapshot(
				user,
				cancellationToken);

			state.Total = snapshot.Total;

			SaveState(state);

			if (state.Total <= 0m ||
				snapshot.Items <= 0)
			{
				return BadRequest(new
				{
					message =
						"Your cart is empty.",

					redirect =
						"/Cart/MyCart"
				});
			}

			string? paymentError = null;
			decimal? walletBalance = null;

			if (state.PaymentMethod ==
				CheckoutPaymentMethod.Balance)
			{
				walletBalance =
					await _wallet.GetBalanceAsync(user.Id);

				if (walletBalance < state.Total)
				{
					paymentError =
						"Insufficient wallet balance.";
				}
			}

			return Ok(new
			{
				shipping = state.Shipping,
				paymentMethod = state.PaymentMethod,
				total = state.Total,
				items = snapshot.Items,
				walletBalance,
				paymentError,
				orderId = state.OrderId
			});
		}

		[HttpPost("PlaceOrder")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PlaceOrder(
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();

			if (user == null)
			{
				return Unauthorized(new
				{
					message =
						"Authentication required."
				});
			}

			CheckoutState state = GetState();

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

			var snapshot = await GetCartSnapshot(
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

			SaveState(state);

			string orderId = state.OrderId;

			switch (state.PaymentMethod.Value)
			{
				case CheckoutPaymentMethod.CashOnDelivery:
					return await PlaceCashOnDeliveryOrder(
						user,
						state,
						orderId,
						cancellationToken);

				case CheckoutPaymentMethod.Balance:
					return await PlaceWalletOrder(
						user,
						state,
						orderId,
						cancellationToken);

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

		[HttpGet("Card")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Card(
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();

			if (user == null)
			{
				return Unauthorized(new
				{
					message =
						"Authentication required."
				});
			}

			CheckoutState state = GetState();

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

			var snapshot = await GetCartSnapshot(
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

			if (string.IsNullOrWhiteSpace(
					_stripePublishableKey) ||
				string.IsNullOrWhiteSpace(
					StripeConfiguration.ApiKey))
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

			SaveState(state);

			long amountInCents =
				ToMinorUnits(state.Total);

			var options =
				new PaymentIntentCreateOptions
				{
					Amount = amountInCents,
					Currency = "usd",

					Description =
						$"PaladinHub order {state.OrderId}",

					PaymentMethodTypes =
						new List<string>
						{
							"card"
						},

					Metadata =
						new Dictionary<string, string>
						{
							["orderId"] =
								state.OrderId,

							["userId"] =
								user.Id
						}
				};

			try
			{
				var service =
					new PaymentIntentService();

				PaymentIntent intent =
					await service.CreateAsync(
						options,
						null,
						cancellationToken);

				if (string.IsNullOrWhiteSpace(
						intent.ClientSecret))
				{
					return StatusCode(
						StatusCodes.Status502BadGateway,
						new
						{
							message =
								"Stripe did not return a client secret."
						});
				}

				return Ok(new
				{
					clientSecret =
						intent.ClientSecret,

					publishableKey =
						_stripePublishableKey,

					paymentIntentId =
						intent.Id,

					orderId =
						state.OrderId,

					amount =
						state.Total,

					currency =
						Currency
				});
			}
			catch (StripeException)
			{
				return StatusCode(
					StatusCodes.Status502BadGateway,
					new
					{
						message =
							"Card payment session could not be created."
					});
			}
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

			User? user = await CurrentUserAsync();

			if (user == null)
			{
				return Unauthorized(new
				{
					message =
						"Authentication required."
				});
			}

			CheckoutState state = GetState();

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

			if (await OrderTransactionExists(
					user.Id,
					orderId,
					cancellationToken))
			{
				await _cartSession.ArchiveAndClear(
					user,
					cancellationToken);

				HttpContext.Session.Remove(SessionKey);

				return Ok(new
				{
					ok = true,
					orderId,

					redirect =
						$"/Checkout/Success?orderId=" +
						Uri.EscapeDataString(orderId)
				});
			}

			PaymentIntent paymentIntent;

			try
			{
				var service =
					new PaymentIntentService();

				paymentIntent =
					await service.GetAsync(
						request.PaymentIntentId.Trim(),
						null,
						null,
						cancellationToken);
			}
			catch (StripeException)
			{
				return StatusCode(
					StatusCodes.Status502BadGateway,
					new
					{
						message =
							"Stripe payment could not be verified."
					});
			}

			if (!string.Equals(
					paymentIntent.Status,
					"succeeded",
					StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest(new
				{
					message =
						"Payment was not completed."
				});
			}

			if (!string.Equals(
					paymentIntent.Currency,
					"usd",
					StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest(new
				{
					message =
						"Payment currency does not match the order."
				});
			}

			if (!paymentIntent.Metadata.TryGetValue(
					"orderId",
					out string? stripeOrderId) ||
				!string.Equals(
					stripeOrderId,
					orderId,
					StringComparison.Ordinal))
			{
				return BadRequest(new
				{
					message =
						"Payment order does not match the checkout order."
				});
			}

			if (!paymentIntent.Metadata.TryGetValue(
					"userId",
					out string? stripeUserId) ||
				!string.Equals(
					stripeUserId,
					user.Id,
					StringComparison.Ordinal))
			{
				return BadRequest(new
				{
					message =
						"Payment user does not match the checkout user."
				});
			}

			var snapshot = await GetCartSnapshot(
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

			long expectedAmount =
				ToMinorUnits(snapshot.Total);

			if (paymentIntent.Amount != expectedAmount)
			{
				return Conflict(new
				{
					message =
						"The cart total changed after the payment session was created."
				});
			}

			state.Total = snapshot.Total;

			SaveState(state);

			await LogPurchaseTransaction(
				user,
				state,
				TransactionStatus.Complete,
				cancellationToken);

			await _cartSession.ArchiveAndClear(
				user,
				cancellationToken);

			HttpContext.Session.Remove(SessionKey);

			return Ok(new
			{
				ok = true,
				orderId,

				redirect =
					$"/Checkout/Success?orderId=" +
					Uri.EscapeDataString(orderId)
			});
		}

		[HttpGet("Registered")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Registered(
			[FromQuery] string? orderId)
		{
			return Ok(new
			{
				orderId =
					orderId?.Trim() ??
					string.Empty,

				status =
					"registered",

				message =
					"Your order was registered successfully."
			});
		}

		[HttpGet("Success")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Success(
			[FromQuery] string? orderId)
		{
			return Ok(new
			{
				orderId =
					orderId?.Trim() ??
					string.Empty,

				status =
					"success",

				message =
					"Payment completed successfully."
			});
		}

		[HttpGet("Failure")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Failure(
			[FromQuery] string? message)
		{
			return Ok(new
			{
				status = "failure",

				message =
					string.IsNullOrWhiteSpace(message)
						? "Payment failed."
						: message.Trim()
			});
		}

		private async Task<IActionResult>
			PlaceCashOnDeliveryOrder(
				User user,
				CheckoutState state,
				string orderId,
				CancellationToken cancellationToken)
		{
			bool alreadyProcessed =
				await OrderTransactionExists(
					user.Id,
					orderId,
					cancellationToken);

			if (!alreadyProcessed)
			{
				await LogPurchaseTransaction(
					user,
					state,
					TransactionStatus.Pending,
					cancellationToken);
			}

			await _cartSession.ArchiveAndClear(
				user,
				cancellationToken);

			HttpContext.Session.Remove(SessionKey);

			return Ok(new
			{
				ok = true,
				orderId,
				redirect = "/Checkout/Registered"
			});
		}

		private async Task<IActionResult> PlaceWalletOrder(
			User user,
			CheckoutState state,
			string orderId,
			CancellationToken cancellationToken)
		{
			bool alreadyProcessed =
				await OrderTransactionExists(
					user.Id,
					orderId,
					cancellationToken);

			if (!alreadyProcessed)
			{
				try
				{
					Guid transactionId =
						await _wallet.ChargeAsync(
							user.Id,
							state.Total,
							$"Order {orderId} (Wallet)");

					await AttachOrderMetadata(
						transactionId,
						orderId,
						cancellationToken);
				}
				catch (InvalidOperationException)
				{
					return BadRequest(new
					{
						message =
							"Insufficient wallet balance.",

						paymentError =
							"Insufficient wallet balance.",

						redirect =
							"/Checkout/Review"
					});
				}
			}

			await _cartSession.ArchiveAndClear(
				user,
				cancellationToken);

			HttpContext.Session.Remove(SessionKey);

			return Ok(new
			{
				ok = true,
				orderId,
				redirect = "/Checkout/Success"
			});
		}

		private Task<User?> CurrentUserAsync()
		{
			return _userManager.GetUserAsync(User);
		}

		private CheckoutState GetState()
		{
			byte[]? bytes =
				HttpContext.Session.Get(SessionKey);

			if (bytes == null ||
				bytes.Length == 0)
			{
				var state =
					new CheckoutState();

				SaveState(state);

				return state;
			}

			try
			{
				return JsonSerializer
					.Deserialize<CheckoutState>(bytes) ??
					new CheckoutState();
			}
			catch (JsonException)
			{
				var state =
					new CheckoutState();

				SaveState(state);

				return state;
			}
		}

		private void SaveState(CheckoutState state)
		{
			HttpContext.Session.Set(
				SessionKey,
				JsonSerializer.SerializeToUtf8Bytes(state));
		}

		private async Task<(int Items, decimal Total)>
			GetCartSnapshot(
				User user,
				CancellationToken cancellationToken)
		{
			await _cartSession.SyncRedisToPersistent(
				user,
				cancellationToken);

			var cart =
				await _productService.GetMyProducts(user);

			return
			(
				cart.MyProducts?.Count ?? 0,
				cart.TotalPrice
			);
		}

		private async Task<bool> OrderTransactionExists(
			string userId,
			string orderId,
			CancellationToken cancellationToken)
		{
			return await _db.Transactions
				.AsNoTracking()
				.AnyAsync(
					transaction =>
						transaction.UserId == userId &&
						transaction.ExternalId == orderId,
					cancellationToken);
		}

		private async Task LogPurchaseTransaction(
			User user,
			CheckoutState state,
			TransactionStatus status,
			CancellationToken cancellationToken)
		{
			if (state.Total <= 0m ||
				string.IsNullOrWhiteSpace(state.OrderId))
			{
				return;
			}

			bool alreadyExists =
				await OrderTransactionExists(
					user.Id,
					state.OrderId,
					cancellationToken);

			if (alreadyExists)
			{
				return;
			}

			var transaction =
				new Transaction
				{
					Id = Guid.NewGuid(),
					UserId = user.Id,
					CreatedAtUtc = DateTime.UtcNow,

					PurchaseTitle =
						$"Order {state.OrderId} " +
						$"({state.PaymentMethod})",

					Amount = state.Total,
					Currency = Currency,
					Region = Region,
					Status = status,
					ExternalId = state.OrderId,
					Type = TransactionType.Purchase
				};

			_db.Transactions.Add(transaction);

			await _db.SaveChangesAsync(
				cancellationToken);
		}

		private async Task AttachOrderMetadata(
			Guid transactionId,
			string orderId,
			CancellationToken cancellationToken)
		{
			Transaction? transaction =
				await _db.Transactions.FirstOrDefaultAsync(
					item => item.Id == transactionId,
					cancellationToken);

			if (transaction == null)
			{
				throw new InvalidOperationException(
					"Wallet transaction was not found.");
			}

			transaction.ExternalId = orderId;
			transaction.Region = Region;

			await _db.SaveChangesAsync(
				cancellationToken);
		}

		private static void NormalizeShipping(
			ShippingInfoVM shipping)
		{
			shipping.FullName =
				shipping.FullName?.Trim() ??
				string.Empty;

			shipping.Address =
				shipping.Address?.Trim() ??
				string.Empty;

			shipping.City =
				shipping.City?.Trim() ??
				string.Empty;

			shipping.PostalCode =
				shipping.PostalCode?.Trim() ??
				string.Empty;

			shipping.Country =
				shipping.Country?.Trim() ??
				string.Empty;

			shipping.Phone =
				shipping.Phone?.Trim() ??
				string.Empty;

			shipping.Email =
				string.IsNullOrWhiteSpace(shipping.Email)
					? null
					: shipping.Email.Trim();
		}

		private static long ToMinorUnits(decimal amount)
		{
			return checked(
				(long)decimal.Round(
					amount * 100m,
					0,
					MidpointRounding.AwayFromZero));
		}

		public sealed class CardFinalizeRequest
		{
			public string PaymentIntentId { get; init; } =
				string.Empty;
		}
	}
}
