using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities;

public sealed class AccessControlUserRow
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? Email { get; set; }
    public string? SecurityStamp { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}

public sealed class AccessControlUserRoleRow
{
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
}
