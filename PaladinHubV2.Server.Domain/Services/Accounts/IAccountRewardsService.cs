using System.Security.Claims;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountRewardsService
	{
		Task<AccountRedeemCodeResult?> RedeemCodeAsync(
			ClaimsPrincipal principal,
			string? code);

		Task<AccountTopUpResult?> TopUpAsync(
			ClaimsPrincipal principal,
			decimal amount);
	}

	public sealed record AccountRedeemCodeResult(
		bool Ok,
		string Reason,
		string Message,
		decimal? Amount,
		string? Currency,
		int? Percent);

	public sealed record AccountTopUpResult(
		bool Ok,
		string Message,
		Guid? TransactionId,
		decimal Amount,
		decimal? Balance,
		string Currency);
}
