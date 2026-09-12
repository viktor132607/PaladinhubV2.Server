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
			User? user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			string regionCode =
				_ui.ReadRegionCookie() ?? "EU";

			string currency =
				_ui.GetCurrencyForRegion(regionCode);

			decimal balance =
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
			User? user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
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

		private Task<User?> GetCurrentUser()
		{
			return _ui.GetMe(User);
		}
	}
}
