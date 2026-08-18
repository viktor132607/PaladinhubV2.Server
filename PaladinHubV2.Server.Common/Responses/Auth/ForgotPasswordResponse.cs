namespace PaladinHubV2.Common.Responses.Auth;

public class ForgotPasswordResponse
{
    public string Message { get; set; } = string.Empty;

    public string? PreviewResetLink { get; set; }
}
