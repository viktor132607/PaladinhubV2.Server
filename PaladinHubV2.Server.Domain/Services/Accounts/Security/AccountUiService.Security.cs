using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed partial class AccountUiService
	{
		public (int score, string[] tips) ComputeSecurityScore(User me) =>
			_securityScore.Compute(me);
	}
}
