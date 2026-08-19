using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountSecurityScoreService
	{
		(int score, string[] tips) Compute(User me);
	}
}
