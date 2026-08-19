using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Payments;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
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
			PaymentMethodsPageData? page =
				await _paymentMethods.GetPageAsync(User);

			if (page == null)
			{
				return AuthenticationRequired();
			}

			return Ok(new
			{
				region = page.Region,
				regionCode = page.RegionCode,
				currency = page.Currency,
				balance = page.Balance,
				methods = page.Methods.Select(method => new
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
			User? user = await GetCurrentUser();

			if (user == null)
			{
				return AuthenticationRequired();
			}

			string publishableKey =
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

			string customerId =
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

			User? user = await GetCurrentUser();

			if (user == null)
			{
				return AuthenticationRequired();
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
		public Task<IActionResult> RemovePaymentMethod(
			[FromQuery] string id)
		{
			return RemovePaymentMethodCore(
				id,
				legacyResponse: true);
		}

		[HttpDelete("PaymentMethods/{id}")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> RemovePaymentMethodApi(
			[FromRoute] string id)
		{
			return RemovePaymentMethodCore(
				id,
				legacyResponse: false);
		}

		[HttpPost("SetDefaultPaymentMethod")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetDefaultPaymentMethod(
			[FromForm] string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return PaymentMethodIdRequired();
			}

			User? user = await GetCurrentUser();

			if (user == null)
			{
				return AuthenticationRequired();
			}

			bool updated =
				await _paymentMethods.SetDefaultPaymentMethod(
					user,
					id.Trim());

			if (!updated)
			{
				return PaymentMethodNotFound();
			}

			return Ok(new
			{
				ok = true,
				message = "Default payment method updated."
			});
		}

		private async Task<IActionResult> RemovePaymentMethodCore(
			string? id,
			bool legacyResponse)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return PaymentMethodIdRequired();
			}

			User? user = await GetCurrentUser();

			if (user == null)
			{
				return AuthenticationRequired();
			}

			bool removed =
				await _paymentMethods.RemovePaymentMethod(
					user,
					id.Trim());

			if (!removed)
			{
				return PaymentMethodNotFound();
			}

			return legacyResponse
				? Ok(new
				{
					ok = true,
					message = "Payment method removed."
				})
				: NoContent();
		}

		private Task<User?> GetCurrentUser()
		{
			return _ui.GetMe(User);
		}

		private IActionResult AuthenticationRequired()
		{
			return Unauthorized(new
			{
				message = "Authentication required."
			});
		}

		private IActionResult PaymentMethodIdRequired()
		{
			return BadRequest(new
			{
				message = "Payment method ID is required."
			});
		}

		private IActionResult PaymentMethodNotFound()
		{
			return NotFound(new
			{
				message = "Payment method not found."
			});
		}
	}
}
