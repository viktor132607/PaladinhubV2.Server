using Microsoft.AspNetCore.Identity;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AuthSessionService
	{
		private readonly UserManager<User> _userManager;
		private readonly EffectivePermissionService? _permissions;

		public AuthSessionService(UserManager<User> userManager)
		{
			_userManager = userManager;
		}

		public AuthSessionService(
			UserManager<User> userManager,
			AppDbContext database)
		{
			_userManager = userManager;
			_permissions = new EffectivePermissionService(database);
		}

		public async Task<AuthSessionResponse> CreateAsync(
			User user)
		{
			IList<string> roles =
				await _userManager.GetRolesAsync(user);
			IReadOnlyList<string> permissions = _permissions is null
				? Array.Empty<string>()
				: await _permissions.GetEffectivePermissionsAsync(user.Id);

			return new AuthSessionResponse(
				IsAuthenticated: true,
				User: new AuthUserResponse(
					user.Id,
					user.UserName ?? string.Empty,
					user.Email ?? string.Empty,
					user.FullName,
					user.AvatarPath,
					roles.ToArray(),
					permissions.ToArray()));
		}
	}
}
