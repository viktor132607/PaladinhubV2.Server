using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Common.Requests.Auth;
using PaladinHubV2.Common.Responses.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public sealed class AuthService : IAuthService
	{
		private const string UserRole = "User";

		private readonly SignInManager<User> _signInManager;
		private readonly UserManager<User> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;

		public AuthService(
			SignInManager<User> signInManager,
			UserManager<User> userManager,
			RoleManager<IdentityRole> roleManager)
		{
			_signInManager = signInManager;
			_userManager = userManager;
			_roleManager = roleManager;
		}

		public async Task<AuthOperationResult> GetCurrentUserAsync(ClaimsPrincipal principal)
		{
			if (principal.Identity?.IsAuthenticated != true)
				return Ok(AuthSessionResponse.Anonymous);

			User? user = await _userManager.GetUserAsync(principal);
			if (user is null)
			{
				await _signInManager.SignOutAsync();
				return Ok(AuthSessionResponse.Anonymous);
			}

			return Ok(await CreateSessionAsync(user));
		}

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

		public async Task<AuthOperationResult> LoginAsync(LoginRequest request)
		{
			string identifier = request.Identifier.Trim();
			if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
				return Unauthorized("Email/username or password is incorrect.");

			User? user = await _userManager.FindByNameAsync(identifier) ??
				await _userManager.FindByEmailAsync(identifier);
			if (user is null)
				return Unauthorized("Email/username or password is incorrect.");

			SignInResult result = await _signInManager.PasswordSignInAsync(
				user,
				request.Password,
				request.RememberMe,
				lockoutOnFailure: true);

			if (result.RequiresTwoFactor)
				return Result(AuthOperationStatus.Accepted, new { requiresTwoFactor = true, rememberMe = request.RememberMe });
			if (result.IsLockedOut)
				return Result(AuthOperationStatus.Locked, new AuthErrorResponse("Your account is temporarily locked."));
			if (result.IsNotAllowed)
				return Result(AuthOperationStatus.Forbidden, new AuthErrorResponse("Login is not allowed for this account."));
			if (!result.Succeeded)
				return Unauthorized("Email/username or password is incorrect.");

			return Ok(await CreateSessionAsync(user));
		}

		public async Task<AuthOperationResult> LoginWithTwoFactorAsync(TwoFactorLoginRequest request)
		{
			User? user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
			if (user is null)
				return Unauthorized("The two-factor login session has expired.");

			string code = Regex.Replace(request.Code, "[^0-9]", string.Empty);
			if (code.Length != 6)
				return BadRequest("Enter a valid 6-digit authenticator code.");

			SignInResult result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
				code,
				request.RememberMe,
				request.RememberMachine);

			if (result.IsLockedOut)
				return Result(AuthOperationStatus.Locked, new AuthErrorResponse("Your account is temporarily locked."));
			if (!result.Succeeded)
				return Unauthorized("Invalid authenticator code.");

			return Ok(await CreateSessionAsync(user));
		}

		public async Task<AuthOperationResult> LoginWithRecoveryCodeAsync(RecoveryCodeLoginRequest request)
		{
			User? user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
			if (user is null)
				return Unauthorized("The recovery-code login session has expired.");

			string code = request.RecoveryCode.Replace(" ", string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(code))
				return BadRequest("Recovery code is required.");

			SignInResult result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(code);
			if (result.IsLockedOut)
				return Result(AuthOperationStatus.Locked, new AuthErrorResponse("Your account is temporarily locked."));
			if (!result.Succeeded)
				return Unauthorized("Invalid recovery code.");

			return Ok(await CreateSessionAsync(user));
		}

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

		public async Task<AuthOperationResult> LogoutAsync()
		{
			await _signInManager.SignOutAsync();
			return Ok(AuthSessionResponse.Anonymous);
		}

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

		private static AuthOperationResult Ok(object payload) => Result(AuthOperationStatus.Ok, payload);
		private static AuthOperationResult BadRequest(string message) => Result(AuthOperationStatus.BadRequest, new AuthErrorResponse(message));
		private static AuthOperationResult Unauthorized(string message) => Result(AuthOperationStatus.Unauthorized, new AuthErrorResponse(message));
		private static AuthOperationResult Conflict(string message) => Result(AuthOperationStatus.Conflict, new AuthErrorResponse(message));
		private static AuthOperationResult Result(AuthOperationStatus status, object payload) => new(status, payload);
	}
}
