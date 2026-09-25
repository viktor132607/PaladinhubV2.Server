using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum TagAdminError
{
    None,
    NotFound,
    Stale,
    Validation,
    InUse,
    RevisionNotFound,
    DeletedRevision
}

public sealed record TagAdminResult(
    TagAdminError Error,
    GameTag? Tag = null,
    string? Message = null);

public interface ITagAdminQueryService
{
    Task<List<TagListItem>> ListAsync(
        CancellationToken cancellationToken);

    Task<List<TagRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface ITagAdminValidator
{
    Task<string?> ValidateAsync(
        int id,
        TagRequest request,
        CancellationToken cancellationToken);
}

public interface ITagUsageGuard
{
    Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface ITagRevisionJournal
{
    void Record(
        GameTag tag,
        string action,
        string actor);

    GameTag ReadSnapshot(
        TagRevision revision);
}
