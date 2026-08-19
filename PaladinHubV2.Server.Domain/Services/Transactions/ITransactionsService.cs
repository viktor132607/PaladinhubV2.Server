using PaladinHub.Models;

namespace PaladinHubV2.Server.Domain.Services.Transactions
{
	public interface ITransactionsService
	{
		Task<TransactionHistoryViewModel> GetHistory(
			string userId,
			string region,
			int page,
			int pageSize);

		Task<TransactionHistoryViewModel> GetHistoryForRequest(
			string userId,
			string? region,
			int page,
			int pageSize);
	}
}
