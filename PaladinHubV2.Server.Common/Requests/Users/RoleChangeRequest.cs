using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Users;

public class RoleChangeRequest
{
    [Required]
    public required Guid UserId { get; set; }
}
