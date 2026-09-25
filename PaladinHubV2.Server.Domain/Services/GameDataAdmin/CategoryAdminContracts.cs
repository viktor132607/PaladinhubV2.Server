using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum CategoryAdminError
{
    None,
    NotFound,
    Stale,
    Validation,
    InUse,
    RevisionNotFound,
    DeletedRevision
}

public sealed record CategoryAdminResult(
    CategoryAdminError Error,
    Category? Category = null,
    string? Message = null);

public interface ICategoryAdminQueryService
{
    Task<List<CategoryListItem>> ListAsync(
        CancellationToken cancellationToken);

    Task<List<CategoryRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface ICategoryAdminValidator
{
    Task<string?> ValidateAsync(
        int id,
        CategoryRequest request,
        CancellationToken cancellationToken);
}

public interface ICategoryUsageGuard
{
    Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface ICategoryRevisionJournal
{
    void Record(
        Category category,
        string action,
        string actor);

    Category ReadSnapshot(
        CategoryRevision revision);
}
