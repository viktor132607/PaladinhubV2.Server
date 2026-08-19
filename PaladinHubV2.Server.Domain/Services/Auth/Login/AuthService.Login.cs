using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Common.Requests.Auth;
using PaladinHubV2.Common.Responses.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public sealed partial class AuthService
	{
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
	}
}
