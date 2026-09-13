using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace PaladinHubV2.Server.Data.Entities;

public sealed class RoleSecurityProfile
{
    [Key]
    public string RoleId { get; set; } = string.Empty;

    public IdentityRole Role { get; set; } = null!;

    public bool IsSystem { get; set; }

    public bool IsDisabled { get; set; }

    [ConcurrencyCheck]
    public int Version { get; set; } = 1;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    public ICollection<RoleSecurityRevision> Revisions { get; set; } = new List<RoleSecurityRevision>();
}

[Index(nameof(RoleId), nameof(PermissionId), IsUnique = true)]
public sealed class RolePermission
{
    public string RoleId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string PermissionId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string GrantedBy { get; set; } = string.Empty;

    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    public RoleSecurityProfile Profile { get; set; } = null!;
}

[Index(nameof(RoleId), nameof(Version), IsUnique = true)]
public sealed class RoleSecurityRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RoleId { get; set; } = string.Empty;

    public int Version { get; set; }

    [MaxLength(30)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Actor { get; set; } = string.Empty;

    public string Snapshot { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public RoleSecurityProfile Profile { get; set; } = null!;
}
