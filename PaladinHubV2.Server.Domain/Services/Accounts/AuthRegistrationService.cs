using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed record AuthRoleAssignmentResult(
		bool Succeeded,
		string? Message = null,
		string[]? Errors = null);

	public sealed class AuthRegistrationService
	{
		private const string UserRole = "User";

		private readonly UserManager<User> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;

		public AuthRegistrationService(
			UserManager<User> userManager,
			RoleManager<IdentityRole> roleManager)
		{
			_userManager = userManager;
			_roleManager = roleManager;
		}

		public User CreateUser(RegisterRequest request)
		{
			int avatarIndex =
				RandomNumberGenerator.GetInt32(1, 40);

			return new User
			{
				UserName = request.Username.Trim(),
				Email = request.Email.Trim(),
				FullName = request.Name.Trim(),
				EmailConfirmed = false,
				AvatarPath =
					$"/images/avatars/default{avatarIndex:00}.png"
			};
		}

		public async Task<AuthRoleAssignmentResult>
			EnsureDefaultRoleAsync(User user)
		{
			if (!await _roleManager.RoleExistsAsync(UserRole))
			{
				IdentityResult createRoleResult =
					await _roleManager.CreateAsync(
						new IdentityRole(UserRole));

				if (!createRoleResult.Succeeded &&
					!await _roleManager.RoleExistsAsync(UserRole))
				{
					return new AuthRoleAssignmentResult(
						false,
						"Could not create the default user role.",
						createRoleResult.Errors
							.Select(error => error.Description)
							.ToArray());
				}
			}

			IdentityResult addRoleResult =
				await _userManager.AddToRoleAsync(
					user,
					UserRole);

			if (!addRoleResult.Succeeded)
			{
				return new AuthRoleAssignmentResult(
					false,
					"Could not assign the default user role.",
					addRoleResult.Errors
						.Select(error => error.Description)
						.ToArray());
			}

			return new AuthRoleAssignmentResult(true);
		}
	}
}
