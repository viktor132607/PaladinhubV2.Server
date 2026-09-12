using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Data.Entities;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public enum AuthLoginError
{
	None,
	InvalidCredentials,
	RequiresTwoFactor,
	LockedOut,
	NotAllowed,
	TwoFactorSessionExpired,
	InvalidTwoFactorCodeFormat,
	InvalidTwoFactorCode,
	RecoverySessionExpired,
	RecoveryCodeRequired,
	InvalidRecoveryCode
}

public sealed record AuthLoginResult(
	AuthLoginError Error,
	AuthSessionResponse? Session = null,
	bool RememberMe = false);

public sealed class AuthLoginService
{
	private readonly SignInManager<User> _signInManager;
	private readonly UserManager<User> _userManager;
	private readonly AuthSessionService _sessionService;

	public AuthLoginService(
		SignInManager<User> signInManager,
		UserManager<User> userManager,
		AuthSessionService sessionService)
	{
		_signInManager = signInManager;
		_userManager = userManager;
		_sessionService = sessionService;
	}

	public async Task<AuthLoginResult> PasswordAsync(
		string identifier,
		string password,
		bool rememberMe)
	{
		string normalizedIdentifier = identifier.Trim();

		if (string.IsNullOrWhiteSpace(normalizedIdentifier) ||
			string.IsNullOrWhiteSpace(password))
		{
			return new AuthLoginResult(
				AuthLoginError.InvalidCredentials);
		}

		User? user =
			await _userManager.FindByNameAsync(normalizedIdentifier) ??
			await _userManager.FindByEmailAsync(normalizedIdentifier);

		if (user is null)
		{
			return new AuthLoginResult(
				AuthLoginError.InvalidCredentials);
		}

		SignInResult result =
			await _signInManager.PasswordSignInAsync(
				user,
				password,
				rememberMe,
				lockoutOnFailure: true);

		if (result.RequiresTwoFactor)
		{
			return new AuthLoginResult(
				AuthLoginError.RequiresTwoFactor,
				RememberMe: rememberMe);
		}

		if (result.IsLockedOut)
		{
			return new AuthLoginResult(AuthLoginError.LockedOut);
		}

		if (result.IsNotAllowed)
		{
			return new AuthLoginResult(AuthLoginError.NotAllowed);
		}

		if (!result.Succeeded)
		{
			return new AuthLoginResult(
				AuthLoginError.InvalidCredentials);
		}

		return new AuthLoginResult(
			AuthLoginError.None,
			await _sessionService.CreateAsync(user));
	}

	public async Task<AuthLoginResult> TwoFactorAsync(
		string rawCode,
		bool rememberMe,
		bool rememberMachine)
	{
		User? user =
			await _signInManager.GetTwoFactorAuthenticationUserAsync();

		if (user is null)
		{
			return new AuthLoginResult(
				AuthLoginError.TwoFactorSessionExpired);
		}

		string code = Regex.Replace(
			rawCode,
			"[^0-9]",
			string.Empty);

		if (code.Length != 6)
		{
			return new AuthLoginResult(
				AuthLoginError.InvalidTwoFactorCodeFormat);
		}

		SignInResult result =
			await _signInManager.TwoFactorAuthenticatorSignInAsync(
				code,
				rememberMe,
				rememberMachine);

		if (result.IsLockedOut)
		{
			return new AuthLoginResult(AuthLoginError.LockedOut);
		}

		if (!result.Succeeded)
		{
			return new AuthLoginResult(
				AuthLoginError.InvalidTwoFactorCode);
		}

		return new AuthLoginResult(
			AuthLoginError.None,
			await _sessionService.CreateAsync(user));
	}

	public async Task<AuthLoginResult> RecoveryCodeAsync(
		string rawCode)
	{
		User? user =
			await _signInManager.GetTwoFactorAuthenticationUserAsync();

		if (user is null)
		{
			return new AuthLoginResult(
				AuthLoginError.RecoverySessionExpired);
		}

		string code = rawCode
			.Replace(" ", string.Empty)
			.Trim();

		if (string.IsNullOrWhiteSpace(code))
		{
			return new AuthLoginResult(
				AuthLoginError.RecoveryCodeRequired);
		}

		SignInResult result =
			await _signInManager.TwoFactorRecoveryCodeSignInAsync(code);

		if (result.IsLockedOut)
		{
			return new AuthLoginResult(AuthLoginError.LockedOut);
		}

		if (!result.Succeeded)
		{
			return new AuthLoginResult(
				AuthLoginError.InvalidRecoveryCode);
		}

		return new AuthLoginResult(
			AuthLoginError.None,
			await _sessionService.CreateAsync(user));
	}
}
