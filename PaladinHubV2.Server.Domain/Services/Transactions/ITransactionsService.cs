using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHub.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PaladinHubV2.Server.Domain.Services.Transactions
{
	public interface ITransactionsService
	{
		Task<TransactionHistoryViewModel> GetHistory(string userId, string region, int page, int pageSize);
	}
}
