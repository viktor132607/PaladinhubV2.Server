using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthCredentialsController : ControllerBase
{
	private readonly SignInManager<User> _signInManager;
	private readonly UserManager<User> _userManager;

	public AuthCredentialsController(
		SignInManager<User> signInManager,
		UserManager<User> userManager)
	{
		_signInManager = signInManager;
		_userManager = userManager;
	}

	[HttpPost("change-password")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> ChangePassword(
		[FromBody] ChangePasswordRequest request)
	{
		User? user = await _userManager.GetUserAsync(User);

		if (user is null)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Authentication required."));
		}

		IdentityResult result =
			await _userManager.ChangePasswordAsync(
				user,
				request.OldPassword,
				request.NewPassword);

		if (!result.Succeeded)
		{
			return BadRequest(
				new AuthErrorResponse(
					"Password update failed.",
					result.Errors
						.Select(error => error.Description)
						.ToArray()));
		}

		await _signInManager.RefreshSignInAsync(user);

		return Ok(new
		{
			message = "Your password has been updated."
		});
	}

	[HttpPost("logout")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Logout()
	{
		await _signInManager.SignOutAsync();
		return Ok(AuthSessionResponse.Anonymous);
	}
}
