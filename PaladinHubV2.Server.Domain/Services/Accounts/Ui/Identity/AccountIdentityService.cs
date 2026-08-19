using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountIdentityService : IAccountIdentityService
	{
		private readonly UserManager<User> _userManager;

		public AccountIdentityService(UserManager<User> userManager)
		{
			_userManager = userManager;
		}

		public async Task<User?> GetMe(ClaimsPrincipal principal)
		{
			if (principal == null)
			{
				return null;
			}

			return await _userManager.GetUserAsync(principal);
		}

		public string? GetUserId(ClaimsPrincipal principal) =>
			principal?.FindFirstValue(ClaimTypes.NameIdentifier);
	}
}
