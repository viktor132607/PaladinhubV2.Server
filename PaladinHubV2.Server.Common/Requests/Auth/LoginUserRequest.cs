using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Auth;

public class LoginUserRequest
{
    [Required]
    public required string Email { get; set; } 
    
    [Required]
    public required string Password { get; set; } 
}
