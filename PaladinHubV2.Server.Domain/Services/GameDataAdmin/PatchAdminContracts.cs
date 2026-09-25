using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum PatchAdminError
{
    None,
    NotFound,
    Stale,
    Validation,
    InUse,
    RevisionNotFound,
    DeletedRevision
}

public sealed record PatchAdminResult(
    PatchAdminError Error,
    GamePatch? Patch = null,
    string? Message = null);

public interface IPatchAdminQueryService
{
    Task<List<PatchListItem>> ListAsync(
        CancellationToken cancellationToken);

    Task<List<PatchRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface IPatchAdminValidator
{
    Task<string?> ValidateAsync(
        int id,
        PatchRequest request,
        CancellationToken cancellationToken);
}

public interface IPatchUsageGuard
{
    Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface IPatchRevisionJournal
{
    void Record(
        GamePatch patch,
        string action,
        string actor);

    GamePatch ReadSnapshot(
        PatchRevision revision);
}
