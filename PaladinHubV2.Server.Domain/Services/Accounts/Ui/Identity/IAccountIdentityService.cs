using System.Security.Claims;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountIdentityService
	{
		Task<User?> GetMe(ClaimsPrincipal principal);
		string? GetUserId(ClaimsPrincipal principal);
	}
}
