using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Payments;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("Account")]
	public sealed class PaymentMethodsController : ControllerBase
	{
		private readonly IPaymentMethodsService _paymentMethods;
		private readonly IAccountUiService _ui;

		public PaymentMethodsController(
			IPaymentMethodsService paymentMethods,
			IAccountUiService ui)
		{
			_paymentMethods = paymentMethods;
			_ui = ui;
		}

		[HttpGet("PaymentMethods")]
		public async Task<IActionResult> PaymentMethods()
		{
			var user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var regionCode =
				_ui.ReadRegionCookie() ?? "EU";

			var currency =
				_ui.GetCurrencyForRegion(regionCode);

			var balance =
				await _ui.GetBalance(user.Id);

			var methods =
				await _paymentMethods.GetMethods(user);

			return Ok(new
			{
				region = _ui.RegionDisplay(regionCode),
				regionCode,
				currency,
				balance,
				methods = methods.Select(method => new
				{
					method.Id,
					method.Brand,
					method.Last4,
					method.Label,
					method.IsDefault,
					method.ExternalId,
					method.Provider,
					method.CreatedAtUtc
				})
			});
		}

		[HttpGet("AddPaymentMethod")]
		public async Task<IActionResult> AddPaymentMethod()
		{
			var user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var publishableKey =
				_paymentMethods.GetStripePublishableKey();

			if (string.IsNullOrWhiteSpace(publishableKey))
			{
				return StatusCode(
					StatusCodes.Status503ServiceUnavailable,
					new
					{
						message =
							"Stripe publishable key is not configured."
					});
			}

			var customerId =
				await _paymentMethods.EnsureStripeCustomer(user);

			return Ok(new
			{
				publishableKey,
				customerId
			});
		}

		[HttpPost("AddPaymentMethodStripe")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddPaymentMethodStripe(
			[FromForm] string paymentMethodId)
		{
			if (string.IsNullOrWhiteSpace(paymentMethodId))
			{
				return BadRequest(new
				{
					message = "Invalid payment method."
				});
			}

			var user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			await _paymentMethods.AddStripePaymentMethod(
				user,
				paymentMethodId.Trim());

			return Ok(new
			{
				ok = true,
				message = "Card added."
			});
		}

		[HttpGet("RemovePaymentMethod")]
		public async Task<IActionResult> RemovePaymentMethod(
			[FromQuery] string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Payment method ID is required."
				});
			}

			var user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var removed =
				await _paymentMethods.RemovePaymentMethod(
					user,
					id.Trim());

			if (!removed)
			{
				return NotFound(new
				{
					message = "Payment method not found."
				});
			}

			return Ok(new
			{
				ok = true,
				message = "Payment method removed."
			});
		}

		[HttpDelete("PaymentMethods/{id}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RemovePaymentMethodApi(
			[FromRoute] string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Payment method ID is required."
				});
			}

			var user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var removed =
				await _paymentMethods.RemovePaymentMethod(
					user,
					id.Trim());

			if (!removed)
			{
				return NotFound(new
				{
					message = "Payment method not found."
				});
			}

			return NoContent();
		}

		[HttpPost("SetDefaultPaymentMethod")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetDefaultPaymentMethod(
			[FromForm] string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Payment method ID is required."
				});
			}

			var user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var updated =
				await _paymentMethods.SetDefaultPaymentMethod(
					user,
					id.Trim());

			if (!updated)
			{
				return NotFound(new
				{
					message = "Payment method not found."
				});
			}

			return Ok(new
			{
				ok = true,
				message = "Default payment method updated."
			});
		}

		private Task<User?> GetCurrentUser()
		{
			return _ui.GetMe(User);
		}
	}
}
