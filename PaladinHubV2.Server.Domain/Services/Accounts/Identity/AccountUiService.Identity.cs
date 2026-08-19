using System.Security.Claims;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed partial class AccountUiService
	{
		public Task<User?> GetMe(ClaimsPrincipal principal) =>
			_identity.GetMe(principal);

		public string? GetUserId(ClaimsPrincipal principal) =>
			_identity.GetUserId(principal);
	}
}
