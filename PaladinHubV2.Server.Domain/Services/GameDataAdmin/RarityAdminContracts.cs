using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum RarityAdminError
{
    None,
    NotFound,
    Stale,
    Validation,
    InUse,
    RevisionNotFound,
    DeletedRevision
}

public sealed record RarityAdminResult(
    RarityAdminError Error,
    ItemRarity? Rarity = null,
    string? Message = null);

public interface IRarityAdminQueryService
{
    Task<List<RarityListItem>> ListAsync(
        CancellationToken cancellationToken);

    Task<List<RarityRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface IRarityAdminValidator
{
    Task<string?> ValidateAsync(
        int id,
        RarityRequest request,
        CancellationToken cancellationToken);
}

public interface IRarityUsageGuard
{
    Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface IRarityRevisionJournal
{
    void Record(
        ItemRarity rarity,
        string action,
        string actor);

    ItemRarity ReadSnapshot(
        RarityRevision revision);
}

public interface IRarityQualitySynchronizer
{
    Task SyncAsync(
        ItemRarity rarity,
        CancellationToken cancellationToken);
}
