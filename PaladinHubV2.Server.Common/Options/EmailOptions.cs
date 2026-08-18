namespace PaladinHubV2.Common.Options;

public class EmailOptions
{
	public const string SectionName = "Email";

	public string DeliveryMode { get; set; } = "Console";

	public string SenderEmail { get; set; } = "noreply@paladinhubv2.local";

	public string SenderName { get; set; } = "PaladinHubV2";

	public string ResendApiKey { get; set; } = string.Empty;

	public string ContactRecipientEmail { get; set; } = string.Empty;

	public int PasswordResetTokenExpiryMinutes { get; set; } = 30;
}
