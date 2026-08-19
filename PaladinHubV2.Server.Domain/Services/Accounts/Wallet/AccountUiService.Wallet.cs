namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed partial class AccountUiService
	{
		public Task<decimal> GetBalance(string userId) =>
			_wallet.GetBalanceAsync(userId);
	}
}
