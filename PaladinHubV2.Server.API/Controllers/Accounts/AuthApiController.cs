using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController]
[Route("api/auth")]
public sealed class AuthApiController : ControllerBase
{
	private const string UserRole = "User";

	private readonly IAntiforgery antiforgery;
	private readonly SignInManager<User> signInManager;
	private readonly UserManager<User> userManager;
	private readonly RoleManager<IdentityRole> roleManager;

	public AuthApiController(
		IAntiforgery antiforgery,
		SignInManager<User> signInManager,
		UserManager<User> userManager,
		RoleManager<IdentityRole> roleManager)
	{
		this.antiforgery = antiforgery;
		this.signInManager = signInManager;
		this.userManager = userManager;
		this.roleManager = roleManager;
	}

	[AllowAnonymous]
	[HttpGet("csrf")]
	[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
	public IActionResult GetCsrfToken()
	{
		AntiforgeryTokenSet tokens =
			antiforgery.GetAndStoreTokens(HttpContext);

		return Ok(new
		{
			token = tokens.RequestToken
		});
	}

	[AllowAnonymous]
	[HttpGet("me")]
	[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
	public async Task<IActionResult> GetCurrentUser()
	{
		if (User.Identity?.IsAuthenticated != true)
		{
			return Ok(AuthSessionResponse.Anonymous);
		}

		User? user = await userManager.GetUserAsync(User);

		if (user is null)
		{
			await signInManager.SignOutAsync();
			return Ok(AuthSessionResponse.Anonymous);
		}

		return Ok(await CreateSessionAsync(user));
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

		if (await userManager.FindByNameAsync(username) is not null)
		{
			return Conflict(
				new AuthErrorResponse("Username is already taken."));
		}

		if (await userManager.FindByEmailAsync(email) is not null)
		{
			return Conflict(
				new AuthErrorResponse("Email is already registered."));
		}

		int avatarIndex = RandomNumberGenerator.GetInt32(1, 40);

		User user = new()
		{
			UserName = username,
			Email = email,
			FullName = fullName,
			EmailConfirmed = false,
			AvatarPath = $"/images/avatars/default{avatarIndex:00}.png"
		};

		IdentityResult createResult =
			await userManager.CreateAsync(user, request.Password);

		if (!createResult.Succeeded)
		{
			return BadRequest(
				new AuthErrorResponse(
					"Registration failed.",
					createResult.Errors
						.Select(error => error.Description)
						.ToArray()));
		}

		IActionResult? roleError = await EnsureUserRoleAsync(user);

		if (roleError is not null)
		{
			await userManager.DeleteAsync(user);
			return roleError;
		}

		await signInManager.SignInAsync(
			user,
			isPersistent: false);

		return Ok(await CreateSessionAsync(user));
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
			await userManager.FindByNameAsync(identifier) ??
			await userManager.FindByEmailAsync(identifier);

		if (user is null)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Email/username or password is incorrect."));
		}

		var result =
			await signInManager.PasswordSignInAsync(
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

		return Ok(await CreateSessionAsync(user));
	}

	[AllowAnonymous]
	[HttpPost("2fa")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithTwoFactor(
		[FromBody] TwoFactorLoginRequest request)
	{
		User? user =
			await signInManager.GetTwoFactorAuthenticationUserAsync();

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

		var result =
			await signInManager.TwoFactorAuthenticatorSignInAsync(
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

		return Ok(await CreateSessionAsync(user));
	}

	[AllowAnonymous]
	[HttpPost("recovery-code")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithRecoveryCode(
		[FromBody] RecoveryCodeLoginRequest request)
	{
		User? user =
			await signInManager.GetTwoFactorAuthenticationUserAsync();

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

		var result =
			await signInManager
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

		return Ok(await CreateSessionAsync(user));
	}

	[Authorize]
	[HttpPost("change-password")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> ChangePassword(
		[FromBody] ChangePasswordRequest request)
	{
		User? user = await userManager.GetUserAsync(User);

		if (user is null)
		{
			return Unauthorized(
				new AuthErrorResponse(
					"Authentication required."));
		}

		IdentityResult result =
			await userManager.ChangePasswordAsync(
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

		await signInManager.RefreshSignInAsync(user);

		return Ok(new
		{
			message = "Your password has been updated."
		});
	}

	[Authorize]
	[HttpPost("logout")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Logout()
	{
		await signInManager.SignOutAsync();
		return Ok(AuthSessionResponse.Anonymous);
	}

	private async Task<IActionResult?> EnsureUserRoleAsync(User user)
	{
		if (!await roleManager.RoleExistsAsync(UserRole))
		{
			IdentityResult createRoleResult =
				await roleManager.CreateAsync(
					new IdentityRole(UserRole));

			if (!createRoleResult.Succeeded &&
				!await roleManager.RoleExistsAsync(UserRole))
			{
				return StatusCode(
					StatusCodes.Status500InternalServerError,
					new AuthErrorResponse(
						"Could not create the default user role.",
						createRoleResult.Errors
							.Select(error => error.Description)
							.ToArray()));
			}
		}

		IdentityResult addRoleResult =
			await userManager.AddToRoleAsync(user, UserRole);

		if (!addRoleResult.Succeeded)
		{
			return StatusCode(
				StatusCodes.Status500InternalServerError,
				new AuthErrorResponse(
					"Could not assign the default user role.",
					addRoleResult.Errors
						.Select(error => error.Description)
						.ToArray()));
		}

		return null;
	}

	private async Task<AuthSessionResponse> CreateSessionAsync(
		User user)
	{
		IList<string> roles =
			await userManager.GetRolesAsync(user);

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

public sealed class LoginRequest
{
	[Required]
	public string Identifier { get; init; } = string.Empty;

	[Required]
	public string Password { get; init; } = string.Empty;

	public bool RememberMe { get; init; }
}

public sealed class RegisterRequest
{
	[Required]
	[StringLength(100, MinimumLength = 2)]
	public string Name { get; init; } = string.Empty;

	[Required]
	[StringLength(32, MinimumLength = 3)]
	[RegularExpression(@"^[a-zA-Z0-9._-]+$")]
	public string Username { get; init; } = string.Empty;

	[Required]
	[EmailAddress]
	public string Email { get; init; } = string.Empty;

	[Required]
	[StringLength(40, MinimumLength = 8)]
	public string Password { get; init; } = string.Empty;

	[Required]
	[Compare(nameof(Password))]
	public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class TwoFactorLoginRequest
{
	[Required]
	public string Code { get; init; } = string.Empty;

	public bool RememberMe { get; init; }

	public bool RememberMachine { get; init; }
}

public sealed class RecoveryCodeLoginRequest
{
	[Required]
	public string RecoveryCode { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
	[Required]
	public string OldPassword { get; init; } = string.Empty;

	[Required]
	[StringLength(40, MinimumLength = 8)]
	public string NewPassword { get; init; } = string.Empty;

	[Required]
	[Compare(nameof(NewPassword))]
	public string ConfirmNewPassword { get; init; } = string.Empty;
}

public sealed record AuthUserResponse(
	string Id,
	string Username,
	string Email,
	string FullName,
	string? AvatarPath,
	string[] Roles);

public sealed record AuthSessionResponse(
	bool IsAuthenticated,
	AuthUserResponse? User)
{
	public static AuthSessionResponse Anonymous { get; } =
		new(false, null);
}

public sealed record AuthErrorResponse(
	string Message,
	string[]? Errors = null);
