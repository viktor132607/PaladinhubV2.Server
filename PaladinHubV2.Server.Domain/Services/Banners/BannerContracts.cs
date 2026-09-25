using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Banners;

public sealed record BannerRequest(
    string InternalName,
    string Title,
    string Text,
    string? ImageUrl,
    string? AltText,
    string? ButtonText,
    string? ButtonUrl,
    string Kind,
    string Position,
    IReadOnlyList<string>? Pages,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    int SortOrder,
    bool IsDismissible,
    bool IsActive,
    int Version);

public sealed record BannerActionRequest(
    int Version,
    string Action,
    Guid? RevisionId);

public sealed record BannerDto(
    Guid Id,
    string InternalName,
    string Title,
    string Text,
    string? ImageUrl,
    string AltText,
    string? ButtonText,
    string? ButtonUrl,
    string Kind,
    string Position,
    IReadOnlyList<string> Pages,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    int SortOrder,
    bool IsDismissible,
    bool IsActive,
    bool IsArchived,
    bool IsDeleted,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record BannerRevisionDto(
    Guid Id,
    Guid BannerId,
    int Version,
    string Action,
    string Actor,
    DateTimeOffset CreatedAtUtc,
    BannerDto Snapshot);

public sealed record BannerStoreResult(
    int Status,
    string Code,
    string Message,
    BannerDto? Banner = null);

public sealed record PublicBannerResult(
    IReadOnlyList<object> Items,
    DateTimeOffset? NextChangeAtUtc);

public interface IBannerRules
{
    string NormalizePath(string? path);
    string? Validate(BannerRequest request, bool creating);
    bool IsVisible(BannerDto banner, string path, DateTimeOffset now);
    BannerDto Create(BannerRequest request, DateTimeOffset now);
    BannerDto Update(BannerDto current, BannerRequest request, DateTimeOffset now);
    BannerRequest ToRequest(BannerDto banner, int version);
}

public interface IBannerRepository
{
    Task<List<BannerDto>> ReadAllAsync(CancellationToken cancellationToken);
    Task<List<BannerRevisionDto>> HistoryAsync(Guid id, CancellationToken cancellationToken);
    Task<BannerDto?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<BannerDto?> RevisionSnapshotAsync(Guid bannerId, Guid revisionId, CancellationToken cancellationToken);
    Task<BannerStoreResult> CreateAsync(BannerDto banner, string actor, CancellationToken cancellationToken);
    Task<BannerStoreResult> ReplaceAsync(
        BannerDto current,
        BannerDto next,
        string action,
        string actor,
        CancellationToken cancellationToken);
}
