namespace PaladinHubV2.Common.Responses.Users;

public class UserResponse 
{
    public required Guid Id { get; set; }
    public required string Email { get; set; } 
    public required string Names { get; set; }
    public required string Phone { get; set; }
    public string? Role { get; set; }

}
