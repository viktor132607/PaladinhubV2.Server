using Microsoft.AspNetCore.Identity;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AuthSessionService
	{
		private readonly UserManager<User> _userManager;

		public AuthSessionService(
			UserManager<User> userManager)
		{
			_userManager = userManager;
		}

		public async Task<AuthSessionResponse> CreateAsync(
			User user)
		{
			IList<string> roles =
				await _userManager.GetRolesAsync(user);

			return new AuthSessionResponse(
				IsAuthenticated: true,
				User: new AuthUserResponse(
					user.Id,
					user.UserName ?? string.Empty,
					user.Email ?? string.Empty,
					user.FullName,
					user.AvatarPath,
					roles.ToArray()));
		}
	}
}
