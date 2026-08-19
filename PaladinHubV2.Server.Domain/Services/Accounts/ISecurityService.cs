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
}
