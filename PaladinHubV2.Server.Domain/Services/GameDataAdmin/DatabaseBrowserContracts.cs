using PaladinHub.Areas.Admin.ViewModels;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum DatabaseBrowseError
{
    None = 0,
    CategoryNotFound,
    DisciplineNotFound
}

public sealed record DatabaseBrowseResult(
    DatabaseBrowseError Error,
    AdminDatabaseIndexViewModel? Model = null);

public sealed record DatabaseBrowseFilters(
    string Search,
    int? CategoryId,
    IReadOnlyCollection<int> CategoryIds,
    int? DisciplineId,
    IReadOnlyCollection<int> DisciplineIds,
    int? TagId,
    int? PatchId,
    int? RarityId);

public interface IDatabaseBrowseScopeResolver
{
    Task<HashSet<int>> ResolveCategoryIdsAsync(
        int? categoryId,
        CancellationToken cancellationToken);

    Task<List<int>?> ResolveDisciplineIdsAsync(
        int? disciplineId,
        CancellationToken cancellationToken);
}

public interface IDatabaseSpellBrowseQuery
{
    Task PopulateAsync(
        AdminDatabaseIndexViewModel model,
        DatabaseBrowseFilters filters,
        CancellationToken cancellationToken);
}

public interface IDatabaseItemBrowseQuery
{
    Task PopulateAsync(
        AdminDatabaseIndexViewModel model,
        DatabaseBrowseFilters filters,
        CancellationToken cancellationToken);
}
