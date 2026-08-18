using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface ISecurityService
	{
		Task ToggleTwoFactor(User me, bool enable);
		Task<bool> GenerateRecoveryCodes(User me, int count);
		Task LogoutAllDevices(User me);
		Task LogoutCurrentSession();
		Task MarkPhoneVerified(User me);
	}

	public sealed class SecurityService : ISecurityService
	{
		private readonly UserManager<User> _userManager;
		private readonly SignInManager<User> _signInManager;

		public SecurityService(
			UserManager<User> userManager,
			SignInManager<User> signInManager)
		{
			_userManager = userManager;
			_signInManager = signInManager;
		}

		public async Task ToggleTwoFactor(User me, bool enable)
		{
			await _userManager.SetTwoFactorEnabledAsync(me, enable);
		}

		public async Task<bool> GenerateRecoveryCodes(
			User me,
			int count)
		{
			if (!await _userManager.GetTwoFactorEnabledAsync(me))
			{
				return false;
			}

			await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(
				me,
				count);

			return true;
		}

		public async Task LogoutAllDevices(User me)
		{
			await _userManager.UpdateSecurityStampAsync(me);
			await _signInManager.SignOutAsync();
		}

		public Task LogoutCurrentSession()
		{
			return _signInManager.SignOutAsync();
		}

		public async Task MarkPhoneVerified(User me)
		{
			me.PhoneNumberConfirmed = true;
			await _userManager.UpdateAsync(me);
		}
	}
}
