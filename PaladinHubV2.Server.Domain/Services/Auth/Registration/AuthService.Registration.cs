using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Common.Requests.Auth;
using PaladinHubV2.Common.Responses.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public sealed partial class AuthService
	{
		public async Task<AuthOperationResult> RegisterAsync(RegisterRequest request)
		{
			string fullName = request.Name.Trim();
			string username = request.Username.Trim();
			string email = request.Email.Trim();

			if (string.IsNullOrWhiteSpace(fullName))
				return BadRequest("Full name is required.");
			if (string.IsNullOrWhiteSpace(username))
				return BadRequest("Username is required.");
			if (string.IsNullOrWhiteSpace(email))
				return BadRequest("Email is required.");

			if (await _userManager.FindByNameAsync(username) is not null)
				return Conflict("Username is already taken.");
			if (await _userManager.FindByEmailAsync(email) is not null)
				return Conflict("Email is already registered.");

			int avatarIndex = RandomNumberGenerator.GetInt32(1, 40);
			User user = new()
			{
				UserName = username,
				Email = email,
				FullName = fullName,
				EmailConfirmed = false,
				AvatarPath = $"/images/avatars/default{avatarIndex:00}.png"
			};

			IdentityResult createResult = await _userManager.CreateAsync(
				user,
				request.Password);

			if (!createResult.Succeeded)
			{
				return Result(
					AuthOperationStatus.BadRequest,
					new AuthErrorResponse(
						"Registration failed.",
						createResult.Errors.Select(error => error.Description).ToArray()));
			}

			AuthOperationResult? roleError = await EnsureUserRoleAsync(user);
			if (roleError is not null)
			{
				await _userManager.DeleteAsync(user);
				return roleError;
			}

			await _signInManager.SignInAsync(user, isPersistent: false);
			return Ok(await CreateSessionAsync(user));
		}
	}
}
