using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController]
[Route("api/auth")]
public sealed class AuthRegistrationController : ControllerBase
{
	private readonly SignInManager<User> _signInManager;
	private readonly UserManager<User> _userManager;
	private readonly AuthRegistrationService _registrationService;
	private readonly AuthSessionService _sessionService;

	public AuthRegistrationController(
		SignInManager<User> signInManager,
		UserManager<User> userManager,
		RoleManager<IdentityRole> roleManager)
	{
		_signInManager = signInManager;
		_userManager = userManager;
		_registrationService =
			new AuthRegistrationService(userManager, roleManager);
		_sessionService = new AuthSessionService(userManager);
	}

	[AllowAnonymous]
	[HttpPost("register")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Register(
		[FromBody] RegisterRequest request)
	{
		string fullName = request.Name.Trim();
		string username = request.Username.Trim();
		string email = request.Email.Trim();

		if (string.IsNullOrWhiteSpace(fullName))
		{
			return BadRequest(
				new AuthErrorResponse("Full name is required."));
		}

		if (string.IsNullOrWhiteSpace(username))
		{
			return BadRequest(
				new AuthErrorResponse("Username is required."));
		}

		if (string.IsNullOrWhiteSpace(email))
		{
			return BadRequest(
				new AuthErrorResponse("Email is required."));
		}

		if (await _userManager.FindByNameAsync(username) is not null)
		{
			return Conflict(
				new AuthErrorResponse("Username is already taken."));
		}

		if (await _userManager.FindByEmailAsync(email) is not null)
		{
			return Conflict(
				new AuthErrorResponse("Email is already registered."));
		}

		User user =
			_registrationService.CreateUser(request);

		IdentityResult createResult =
			await _userManager.CreateAsync(
				user,
				request.Password);

		if (!createResult.Succeeded)
		{
			return BadRequest(
				new AuthErrorResponse(
					"Registration failed.",
					createResult.Errors
						.Select(error => error.Description)
						.ToArray()));
		}

		AuthRoleAssignmentResult roleResult =
			await _registrationService
				.EnsureDefaultRoleAsync(user);

		if (!roleResult.Succeeded)
		{
			await _userManager.DeleteAsync(user);

			return StatusCode(
				StatusCodes.Status500InternalServerError,
				new AuthErrorResponse(
					roleResult.Message ??
						"Could not assign the default user role.",
					roleResult.Errors));
		}

		await _signInManager.SignInAsync(
			user,
			isPersistent: false);

		return Ok(await _sessionService.CreateAsync(user));
	}
}
