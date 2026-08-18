using System.Security.Claims;
using PaladinHub.Models.Account;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountOverviewService
	{
		Task<MyAccountViewModel?> GetMyAccountAsync(
			ClaimsPrincipal principal,
			CancellationToken cancellationToken = default);

		Task<MyAccountViewModel?> GetOverviewAsync(
			ClaimsPrincipal principal,
			int page,
			CancellationToken cancellationToken = default);
	}
}
