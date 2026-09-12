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
	public sealed class PaymentMethodMutationsController : ControllerBase
	{
		private readonly IPaymentMethodsService _paymentMethods;
		private readonly IAccountUiService _ui;

		public PaymentMethodMutationsController(
			IPaymentMethodsService paymentMethods,
			IAccountUiService ui)
		{
			_paymentMethods = paymentMethods;
			_ui = ui;
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
				return InvalidPaymentMethodId();
			}

			User? user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			bool removed =
				await _paymentMethods.RemovePaymentMethod(
					user,
					id.Trim());

			if (!removed)
			{
				return PaymentMethodNotFound();
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
				return InvalidPaymentMethodId();
			}

			User? user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			bool removed =
				await _paymentMethods.RemovePaymentMethod(
					user,
					id.Trim());

			if (!removed)
			{
				return PaymentMethodNotFound();
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
				return InvalidPaymentMethodId();
			}

			User? user = await GetCurrentUser();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
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

		private Task<User?> GetCurrentUser()
		{
			return _ui.GetMe(User);
		}

		private IActionResult InvalidPaymentMethodId()
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
