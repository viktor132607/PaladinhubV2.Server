using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public enum TwoFactorSetupError
	{
		None = 0,
		MustDisableBeforeReset,
		KeyGenerationFailed,
		KeyLoadFailed
	}

	public sealed record TwoFactorSetupResult(
		TwoFactorSetupError Error,
		bool TwoFactorEnabled,
		string? SharedKey = null,
		string? AuthenticatorUri = null,
		string? QrCodeUrl = null,
		string[]? Errors = null);

	public enum TwoFactorEnableError
	{
		None = 0,
		AlreadyEnabled,
		InvalidCodeFormat,
		InvalidCode
	}

	public sealed record TwoFactorEnableResult(
		TwoFactorEnableError Error,
		string[] RecoveryCodes);

	public sealed class AccountTwoFactorService
	{
		private readonly ISecurityService _security;
		private readonly UserManager<User> _userManager;

		public AccountTwoFactorService(
			ISecurityService security,
			UserManager<User> userManager)
		{
			_security = security;
			_userManager = userManager;
		}

		public async Task<TwoFactorSetupResult> PrepareSetupAsync(
			User user,
			bool reset)
		{
			if (await AccountFactors.AuthenticatorAsync(_userManager, user))
			{
				if (reset)
				{
					return new TwoFactorSetupResult(
						TwoFactorSetupError.MustDisableBeforeReset,
						true);
				}

				return new TwoFactorSetupResult(
					TwoFactorSetupError.None,
					true);
			}

			string? key =
				await _userManager.GetAuthenticatorKeyAsync(user);

			if (reset || string.IsNullOrWhiteSpace(key))
			{
				IdentityResult resetResult =
					await _userManager.ResetAuthenticatorKeyAsync(user);

				if (!resetResult.Succeeded)
				{
					return new TwoFactorSetupResult(
						TwoFactorSetupError.KeyGenerationFailed,
						false,
						Errors: resetResult.Errors
							.Select(error => error.Description)
							.ToArray());
				}

				key = await _userManager.GetAuthenticatorKeyAsync(user);
			}

			if (string.IsNullOrWhiteSpace(key))
			{
				return new TwoFactorSetupResult(
					TwoFactorSetupError.KeyLoadFailed,
					false);
			}

			string issuer = Uri.EscapeDataString("PaladinHub");
			string accountName = Uri.EscapeDataString(
				user.Email ?? user.UserName ?? user.Id);

			string authenticatorUri =
				$"otpauth://totp/{issuer}:{accountName}" +
				$"?secret={key}" +
				$"&issuer={issuer}" +
				"&digits=6" +
				"&algorithm=SHA1" +
				"&period=30";

			// Never disclose the authenticator secret to an external QR service.
			string? qrCodeUrl = null;

			return new TwoFactorSetupResult(
				TwoFactorSetupError.None,
				false,
				FormatKey(key),
				authenticatorUri,
				qrCodeUrl);
		}

		public async Task<TwoFactorEnableResult> EnableAsync(
			User user,
			string? code)
		{
			if (await AccountFactors.AuthenticatorAsync(_userManager, user))
			{
				return new TwoFactorEnableResult(
					TwoFactorEnableError.AlreadyEnabled,
					Array.Empty<string>());
			}

			string sanitizedCode = Regex.Replace(
				code ?? string.Empty,
				"[^0-9]",
				string.Empty);

			if (sanitizedCode.Length != 6)
			{
				return new TwoFactorEnableResult(
					TwoFactorEnableError.InvalidCodeFormat,
					Array.Empty<string>());
			}

			bool valid =
				await _userManager.VerifyTwoFactorTokenAsync(
					user,
					TokenOptions.DefaultAuthenticatorProvider,
					sanitizedCode);

			if (!valid)
			{
				return new TwoFactorEnableResult(
					TwoFactorEnableError.InvalidCode,
					Array.Empty<string>());
			}

			AccountFactors.Ensure(await _userManager.SetAuthenticationTokenAsync(user, AccountFactors.Store, "Authenticator2FA", "true"));
			await _security.ToggleTwoFactor(user, true);

			IEnumerable<string>? generatedCodes =
				await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(
					user,
					10);

			return new TwoFactorEnableResult(
				TwoFactorEnableError.None,
				generatedCodes?.ToArray() ?? Array.Empty<string>());
		}

        public async Task<bool> CheckPasswordAsync(User user, string password)
        {
            if (string.IsNullOrWhiteSpace(password) || await _userManager.IsLockedOutAsync(user)) return false;
            if (!await _userManager.CheckPasswordAsync(user, password))
            {
                await _userManager.AccessFailedAsync(user);
                return false;
            }
            await _userManager.ResetAccessFailedCountAsync(user);
            return true;
        }

		public async Task DisableAsync(User user)
		{
			ArgumentNullException.ThrowIfNull(user);

			if (user.TwoFactorEnabled)
			{
				AccountFactors.Ensure(await _userManager.SetAuthenticationTokenAsync(user, AccountFactors.Store, "Authenticator2FA", "false"));
				await _security.ToggleTwoFactor(user, await AccountFactors.EmailAsync(_userManager, user));
			}
		}

		public async Task<string[]?> GenerateRecoveryCodesAsync(
			User user)
		{
			if (!user.TwoFactorEnabled)
			{
				return null;
			}

			IEnumerable<string>? generatedCodes =
				await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(
					user,
					10);

			return generatedCodes?.ToArray() ?? Array.Empty<string>();
		}

		private static string FormatKey(string key)
		{
			string normalizedKey = key.ToUpperInvariant();

			return Regex.Replace(
				normalizedKey,
				".{4}",
				"$0 ")
				.Trim();
		}
	}
}
