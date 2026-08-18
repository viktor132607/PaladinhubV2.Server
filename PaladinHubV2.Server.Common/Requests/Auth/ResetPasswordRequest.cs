using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Auth;

public class ResetPasswordRequest
{
    [Required]
    public required string Token { get; set; }

    [Required]
    [MinLength(8)]
    public required string NewPassword { get; set; }
}
