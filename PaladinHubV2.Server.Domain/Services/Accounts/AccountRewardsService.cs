using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Promos;
using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountRewardsService : IAccountRewardsService
	{
		private const string Currency = "USD";
		private const string DiscountSessionKey =
			"cart_discount_percent";

		private readonly IAccountUiService _ui;
		private readonly IPromoCodeService _promo;
		private readonly IWalletService _wallet;
		private readonly IHttpContextAccessor _httpContextAccessor;

		public AccountRewardsService(
			IAccountUiService ui,
			IPromoCodeService promo,
			IWalletService wallet,
			IHttpContextAccessor httpContextAccessor)
		{
			_ui = ui;
			_promo = promo;
			_wallet = wallet;
			_httpContextAccessor = httpContextAccessor;
		}

		public async Task<AccountRedeemCodeResult?> RedeemCodeAsync(
			ClaimsPrincipal principal,
			string? code)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null)
			{
				return null;
			}

			if (string.IsNullOrWhiteSpace(code))
			{
				return new AccountRedeemCodeResult(
					false,
					"empty",
					"Code is required.",
					null,
					null,
					null);
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

			if (result.ok && result.percent.HasValue)
			{
				_httpContextAccessor.HttpContext?.Session.SetInt32(
					DiscountSessionKey,
					result.percent.Value);
			}

			return new AccountRedeemCodeResult(
				result.ok,
				reason,
				result.msg,
				result.amount,
				result.currency,
				result.percent);
		}

		public async Task<AccountTopUpResult?> TopUpAsync(
			ClaimsPrincipal principal,
			decimal amount)
		{
			User? me = await _ui.GetMe(principal);
			if (me == null)
			{
				return null;
			}

			if (amount <= 0m)
			{
				return new AccountTopUpResult(
					false,
					"Amount must be greater than zero.",
					null,
					amount,
					null,
					Currency);
			}

			Guid transactionId = await _wallet.TopUpAsync(
				me.Id,
				amount,
				"Balance Top-up");

			decimal balance = await _wallet.GetBalanceAsync(me.Id);

			return new AccountTopUpResult(
				true,
				"Balance topped up successfully.",
				transactionId,
				amount,
				balance,
				Currency);
		}
	}
}
