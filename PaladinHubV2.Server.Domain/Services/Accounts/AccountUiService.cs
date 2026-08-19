using PaladinHubV2.Server.Domain.Services.Wallet;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed partial class AccountUiService : IAccountUiService
	{
		private readonly IAccountIdentityService _identity;
		private readonly IAccountSecurityScoreService _securityScore;
		private readonly IAccountRegionService _region;
		private readonly IAccountAvatarDiscoveryService _avatars;
		private readonly IWalletService _wallet;

		public AccountUiService(
			IAccountIdentityService identity,
			IAccountSecurityScoreService securityScore,
			IAccountRegionService region,
			IAccountAvatarDiscoveryService avatars,
			IWalletService wallet)
		{
			_identity = identity;
			_securityScore = securityScore;
			_region = region;
			_avatars = avatars;
			_wallet = wallet;
		}
	}
}
