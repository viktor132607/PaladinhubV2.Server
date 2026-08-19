using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Common.Responses.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public sealed partial class AuthService
	{
		private async Task<AuthOperationResult?> EnsureUserRoleAsync(User user)
		{
			if (!await _roleManager.RoleExistsAsync(UserRole))
			{
				IdentityResult createRoleResult = await _roleManager.CreateAsync(new IdentityRole(UserRole));
				if (!createRoleResult.Succeeded && !await _roleManager.RoleExistsAsync(UserRole))
				{
					return Result(
						AuthOperationStatus.InternalError,
						new AuthErrorResponse(
							"Could not create the default user role.",
							createRoleResult.Errors.Select(error => error.Description).ToArray()));
				}
			}

			IdentityResult addRoleResult = await _userManager.AddToRoleAsync(user, UserRole);
			if (!addRoleResult.Succeeded)
			{
				return Result(
					AuthOperationStatus.InternalError,
					new AuthErrorResponse(
						"Could not assign the default user role.",
						addRoleResult.Errors.Select(error => error.Description).ToArray()));
			}

			return null;
		}

		private async Task<AuthSessionResponse> CreateSessionAsync(User user)
		{
			IList<string> roles = await _userManager.GetRolesAsync(user);
			return new AuthSessionResponse(
				true,
				new AuthUserResponse(
					user.Id,
					user.UserName ?? string.Empty,
					user.Email ?? string.Empty,
					user.FullName,
					user.AvatarPath,
					roles.ToArray()));
		}

		private static AuthOperationResult Ok(object payload) =>
			Result(AuthOperationStatus.Ok, payload);

		private static AuthOperationResult BadRequest(string message) =>
			Result(AuthOperationStatus.BadRequest, new AuthErrorResponse(message));

		private static AuthOperationResult Unauthorized(string message) =>
			Result(AuthOperationStatus.Unauthorized, new AuthErrorResponse(message));

		private static AuthOperationResult Conflict(string message) =>
			Result(AuthOperationStatus.Conflict, new AuthErrorResponse(message));

		private static AuthOperationResult Result(
			AuthOperationStatus status,
			object payload) => new(status, payload);
	}
}
