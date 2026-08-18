using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Auth;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}
