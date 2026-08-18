namespace PaladinHubV2.Common.Responses.Auth;

public class TokenResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public string? Names { get; set; }

    public string? Phone { get; set; }

    public string? Role { get; set; }
}
