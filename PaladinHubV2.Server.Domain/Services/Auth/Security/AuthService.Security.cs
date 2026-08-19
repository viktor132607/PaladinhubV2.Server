using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Common.Requests.Auth;
using PaladinHubV2.Common.Responses.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public sealed partial class AuthService
	{
		public async Task<AuthOperationResult> ChangePasswordAsync(
			ClaimsPrincipal principal,
			ChangePasswordRequest request)
		{
			User? user = await _userManager.GetUserAsync(principal);
			if (user is null)
				return Unauthorized("Authentication required.");

			IdentityResult result = await _userManager.ChangePasswordAsync(
				user,
				request.OldPassword,
				request.NewPassword);

			if (!result.Succeeded)
			{
				return Result(
					AuthOperationStatus.BadRequest,
					new AuthErrorResponse(
						"Password update failed.",
						result.Errors.Select(error => error.Description).ToArray()));
			}

			await _signInManager.RefreshSignInAsync(user);
			return Ok(new { message = "Your password has been updated." });
		}
	}
}
