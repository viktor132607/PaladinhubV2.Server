using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface ISecurityService
	{
		Task ToggleTwoFactor(User me, bool enable);
		Task<bool> GenerateRecoveryCodes(User me, int count);
		Task LogoutAllDevices(User me);
	}

	public class SecurityService : ISecurityService
	{
		private readonly UserManager<User> _um;
		private readonly SignInManager<User> _sm;

		public SecurityService(UserManager<User> um, SignInManager<User> sm)
		{
			_um = um;
			_sm = sm;
		}

		public async Task ToggleTwoFactor(User me, bool enable)
		{
			await _um.SetTwoFactorEnabledAsync(me, enable);
		}

		public async Task<bool> GenerateRecoveryCodes(User me, int count)
		{
			if (!await _um.GetTwoFactorEnabledAsync(me)) return false;
			await _um.GenerateNewTwoFactorRecoveryCodesAsync(me, count);
			return true;
		}

		public async Task LogoutAllDevices(User me)
		{
			await _um.UpdateSecurityStampAsync(me);
			await _sm.SignOutAsync();
		}
	}
}
