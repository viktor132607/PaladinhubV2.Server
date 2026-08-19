using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountSecurityScoreService : IAccountSecurityScoreService
	{
		public (int score, string[] tips) Compute(User me)
		{
			int score = 0;
			var tips = new List<string>();

			if (!string.IsNullOrWhiteSpace(me.Email))
			{
				score += me.EmailConfirmed ? 30 : 10;
				if (!me.EmailConfirmed)
				{
					tips.Add("Verify your email.");
				}
			}

			if (!string.IsNullOrWhiteSpace(me.PhoneNumber))
			{
				score += 15;
			}
			else
			{
				tips.Add("Add a phone number as a recovery factor.");
			}

			if (me.TwoFactorEnabled)
			{
				score += 40;
			}
			else
			{
				tips.Add("Enable two-factor authentication.");
			}

			if (!string.IsNullOrWhiteSpace(me.PasswordHash))
			{
				score += 15;
			}
			else
			{
				tips.Add("Set a strong account password.");
			}

			return (Math.Clamp(score, 0, 100), tips.ToArray());
		}
	}
}
