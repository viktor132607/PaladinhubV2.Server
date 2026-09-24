using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class AccountSecurityScorer : IAccountSecurityScorer
{
    public (int score, string[] tips) Compute(User user)
    {
        int score = 0;
        List<string> tips = [];

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            score += user.EmailConfirmed ? 30 : 10;

            if (!user.EmailConfirmed)
            {
                tips.Add("Verify your email.");
            }
        }

        if (user.TwoFactorEnabled)
        {
            score += 40;
        }
        else
        {
            tips.Add("Enable two-factor authentication.");
        }

        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            score += 30;
        }
        else
        {
            tips.Add("Set a strong account password.");
        }

        return (Math.Clamp(score, 0, 100), tips.ToArray());
    }
}
