using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum MediaAdminError
{
    None,
    Validation,
    NotFound,
    Stale,
    InUse,
    RevisionNotFound,
    DeletedRevision
}

public sealed record MediaAdminResult(
    MediaAdminError Error,
    Guid? Id = null,
    string? Message = null);

public sealed record MediaSnapshot(
    string Name,
    string AltText,
    string Description,
    bool IsArchived,
    bool IsDeleted);

public interface IMediaAdminQueryService
{
    Task<MediaPageResult> ListAsync(
        string? search,
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<List<MediaRevision>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken);
}

public interface IMediaAdminValidator
{
    string? Validate(MediaRequest request);
}

public interface IMediaUsageCounter
{
    Task<int> CountAsync(
        Guid id,
        CancellationToken cancellationToken);
}

public interface IMediaBannerUsageLookup
{
    Task<int> CountAsync(
        Guid id,
        CancellationToken cancellationToken);
}

public interface IMediaRevisionJournal
{
    void Record(
        SpellIcon media,
        string action,
        string actor);

    MediaSnapshot? ReadSnapshot(
        MediaRevision revision);
}
