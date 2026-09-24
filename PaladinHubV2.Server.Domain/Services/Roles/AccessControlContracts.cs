namespace PaladinHubV2.Server.Domain.Services.Roles;

public enum AccessControlFailure
{
    Validation,
    NotFound,
    Conflict
}

public sealed class AccessControlAdminException : Exception
{
    public AccessControlAdminException(
        AccessControlFailure failure,
        string message)
        : base(message)
    {
        Failure = failure;
    }

    public AccessControlFailure Failure { get; }
}

public sealed record AccessControlActor(string Id, string Name);
public sealed record CreateRoleRequest(
    string Name,
    IReadOnlyCollection<string>? Permissions);
public sealed record UpdateRoleRequest(
    string Name,
    bool IsDisabled,
    int Version);
public sealed record ReplaceRolePermissionsRequest(
    IReadOnlyCollection<string> Permissions,
    int Version);
public sealed record RestoreRoleRevisionRequest(
    int RevisionVersion,
    int Version);

public sealed record RoleUserSummary(
    string Id,
    string UserName,
    string? Email);
public sealed record UserRoleSummary(
    string Id,
    string UserName,
    string? Email,
    IReadOnlyList<string> Roles);
public sealed record RoleSecurityRevisionResponse(
    int Version,
    string Action,
    string Actor,
    DateTime CreatedAtUtc,
    string Snapshot);
public sealed record AccessControlAuditResponse(
    Guid Id,
    string Action,
    string ActorId,
    string Actor,
    string? TargetRoleId,
    string? TargetRoleName,
    string? TargetUserId,
    string? TargetUserName,
    DateTime CreatedAtUtc,
    string OldState,
    string NewState);

public sealed record RoleResponse(
    string Id,
    string Name,
    bool IsSystem,
    bool IsDisabled,
    int Version,
    int UserCount,
    IReadOnlyList<string> Permissions,
    bool IsDeleted = false);

internal sealed record RoleSnapshot(
    string Name,
    bool IsDisabled,
    string[] Permissions);

internal sealed record MembershipSnapshot(
    string UserId,
    string RoleId,
    bool Assigned);
