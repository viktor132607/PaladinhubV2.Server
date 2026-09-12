using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Promos;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountWalletController : ControllerBase
	{
		private const string Currency = "USD";

		private readonly IAccountUiService _ui;
		private readonly IPromoCodeService _promo;
		private readonly IWalletService _wallet;

		public AccountWalletController(
			IAccountUiService ui,
			IPromoCodeService promo,
			IWalletService wallet)
		{
			_ui = ui;
			_promo = promo;
			_wallet = wallet;
		}

		[HttpPost("RedeemCode")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RedeemCode(
			[FromForm] string code)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (string.IsNullOrWhiteSpace(code))
			{
				return BadRequest(new
				{
					ok = false,
					reason = "empty",
					message = "Code is required."
				});
			}

			var result = await _promo.RedeemAsync(
				me,
				code,
				Currency);

			string reason = result.ok
				? "success"
				: result.msg.Contains(
					"already",
					StringComparison.OrdinalIgnoreCase)
					? "already-used"
					: "invalid";

			if (!result.ok)
			{
				var error = new
				{
					ok = false,
					reason,
					message = result.msg,
					amount = result.amount,
					currency = result.currency,
					percent = result.percent
				};

				return reason == "already-used"
					? Conflict(error)
					: BadRequest(error);
			}

			if (result.percent.HasValue)
			{
				HttpContext.Session.SetInt32(
					"cart_discount_percent",
					result.percent.Value);
			}

			return Ok(new
			{
				ok = true,
				reason,
				message = result.msg,
				amount = result.amount,
				currency = result.currency,
				percent = result.percent
			});
		}

		[HttpPost("DevTopUp")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DevTopUp(
			[FromForm] decimal amount)
		{
			if (amount <= 0m)
			{
				return BadRequest(new
				{
					message =
						"Amount must be greater than zero."
				});
			}

			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			Guid transactionId =
				await _wallet.TopUpAsync(
					me.Id,
					amount,
					"Balance Top-up");

			decimal balance =
				await _wallet.GetBalanceAsync(me.Id);

			return Ok(new
			{
				ok = true,
				transactionId,
				amount,
				balance,
				currency = Currency
			});
		}
	}
}
