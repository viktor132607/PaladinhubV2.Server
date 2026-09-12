using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;
using System.Text.RegularExpressions;
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
public sealed class AuthLoginController : ControllerBase
{
	private readonly SignInManager<User> _signInManager;
	private readonly UserManager<User> _userManager;
	private readonly AuthSessionService _sessionService;

	public AuthLoginController(
		SignInManager<User> signInManager,
		UserManager<User> userManager)
	{
		_signInManager = signInManager;
		_userManager = userManager;
		_sessionService = new AuthSessionService(userManager);
	}

	[AllowAnonymous]
	[HttpPost("login")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Login(
		[FromBody] LoginRequest request)
	{
		string identifier = request.Identifier.Trim();

		if (string.IsNullOrWhiteSpace(identifier) ||
			string.IsNullOrWhiteSpace(request.Password))
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Email/username or password is incorrect."));
		}

		User? user =
			await _userManager.FindByNameAsync(identifier) ??
			await _userManager.FindByEmailAsync(identifier);

		if (user is null)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Email/username or password is incorrect."));
		}

		SignInResult result =
			await _signInManager.PasswordSignInAsync(
				user,
				request.Password,
				request.RememberMe,
				lockoutOnFailure: true);

		if (result.RequiresTwoFactor)
		{
			return Accepted(new
			{
				requiresTwoFactor = true,
				rememberMe = request.RememberMe
			});
		}

		if (result.IsLockedOut)
		{
			return StatusCode(
				StatusCodes.Status423Locked,
				new AuthErrorResponse(
					"Your account is temporarily locked."));
		}

		if (result.IsNotAllowed)
		{
			return StatusCode(
				StatusCodes.Status403Forbidden,
				new AuthErrorResponse(
					"Login is not allowed for this account."));
		}

		if (!result.Succeeded)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Email/username or password is incorrect."));
		}

		return Ok(await _sessionService.CreateAsync(user));
	}

	[AllowAnonymous]
	[HttpPost("2fa")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithTwoFactor(
		[FromBody] TwoFactorLoginRequest request)
	{
		User? user =
			await _signInManager.GetTwoFactorAuthenticationUserAsync();

		if (user is null)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"The two-factor login session has expired."));
		}

		string code = Regex.Replace(
			request.Code,
			"[^0-9]",
			string.Empty);

		if (code.Length != 6)
		{
			return BadRequest(
				new AuthErrorResponse(
					"Enter a valid 6-digit authenticator code."));
		}

		SignInResult result =
			await _signInManager.TwoFactorAuthenticatorSignInAsync(
				code,
				request.RememberMe,
				request.RememberMachine);

		if (result.IsLockedOut)
		{
			return StatusCode(
				StatusCodes.Status423Locked,
				new AuthErrorResponse(
					"Your account is temporarily locked."));
		}

		if (!result.Succeeded)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Invalid authenticator code."));
		}

		return Ok(await _sessionService.CreateAsync(user));
	}

	[AllowAnonymous]
	[HttpPost("recovery-code")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithRecoveryCode(
		[FromBody] RecoveryCodeLoginRequest request)
	{
		User? user =
			await _signInManager.GetTwoFactorAuthenticationUserAsync();

		if (user is null)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"The recovery-code login session has expired."));
		}

		string code = request.RecoveryCode
			.Replace(" ", string.Empty)
			.Trim();

		if (string.IsNullOrWhiteSpace(code))
		{
			return BadRequest(
				new AuthErrorResponse(
					"Recovery code is required."));
		}

		SignInResult result =
			await _signInManager
				.TwoFactorRecoveryCodeSignInAsync(code);

		if (result.IsLockedOut)
		{
			return StatusCode(
				StatusCodes.Status423Locked,
				new AuthErrorResponse(
					"Your account is temporarily locked."));
		}

		if (!result.Succeeded)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Invalid recovery code."));
		}

		return Ok(await _sessionService.CreateAsync(user));
	}
}
