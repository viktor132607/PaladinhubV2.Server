namespace PaladinHubV2.Server.Domain.Services.Wallet
{
	public interface IWalletService
	{
		Task<decimal> GetBalanceAsync(string userId);
		Task<System.Guid> TopUpAsync(string userId, decimal amount, string title = "Balance Top-up");
		Task<System.Guid> ChargeAsync(string userId, decimal amount, string title);
	}
}
