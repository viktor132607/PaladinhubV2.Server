using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DatabaseBrowserService
{
    private readonly IDatabaseBrowseScopeResolver _scopes;
    private readonly IDatabaseSpellBrowseQuery _spells;
    private readonly IDatabaseItemBrowseQuery _items;

    public DatabaseBrowserService(
        AppDbContext db)
        : this(
            new DatabaseBrowseScopeResolver(db),
            new DatabaseSpellBrowseQuery(db),
            new DatabaseItemBrowseQuery(db))
    {
    }

    public DatabaseBrowserService(
        IDatabaseBrowseScopeResolver scopes,
        IDatabaseSpellBrowseQuery spells,
        IDatabaseItemBrowseQuery items)
    {
        _scopes = scopes;
        _spells = spells;
        _items = items;
    }

    public async Task<DatabaseBrowseResult> BrowseAsync(
        string? entity,
        string? search,
        int page,
        int pageSize,
        int? categoryId,
        int? disciplineId,
        int? tagId,
        int? patchId,
        int? rarityId,
        CancellationToken cancellationToken)
    {
        AdminEntity selectedEntity =
            ParseEntity(entity);

        string normalizedSearch =
            search?.Trim() ??
            string.Empty;

        HashSet<int> categoryIds =
            await _scopes.ResolveCategoryIdsAsync(
                categoryId,
                cancellationToken);

        if (categoryId > 0 &&
            categoryIds.Count == 0)
        {
            return new DatabaseBrowseResult(
                DatabaseBrowseError.CategoryNotFound);
        }

        List<int>? disciplineIds =
            await _scopes.ResolveDisciplineIdsAsync(
                disciplineId,
                cancellationToken);

        if (disciplineId > 0 &&
            disciplineIds is null)
        {
            return new DatabaseBrowseResult(
                DatabaseBrowseError.DisciplineNotFound);
        }

        var model =
            new AdminDatabaseIndexViewModel
            {
                Entity = selectedEntity,
                Search = normalizedSearch,
                Page = Math.Max(page, 1),
                PageSize =
                    Math.Clamp(
                        pageSize,
                        1,
                        100)
            };

        var filters =
            new DatabaseBrowseFilters(
                normalizedSearch,
                categoryId,
                categoryIds,
                disciplineId,
                disciplineIds ?? [],
                tagId,
                patchId,
                rarityId);

        if (selectedEntity ==
            AdminEntity.Spells)
        {
            await _spells.PopulateAsync(
                model,
                filters,
                cancellationToken);
        }
        else
        {
            await _items.PopulateAsync(
                model,
                filters,
                cancellationToken);
        }

        return new DatabaseBrowseResult(
            DatabaseBrowseError.None,
            model);
    }

    internal static void ClampPage(
        AdminDatabaseIndexViewModel model)
    {
        int totalPages =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    model.Total /
                    (double)model.PageSize));

        model.Page =
            Math.Min(
                model.Page,
                totalPages);
    }

    internal static AdminEntity ParseEntity(
        string? entity)
    {
        return string.Equals(
            entity?.Trim(),
            nameof(AdminEntity.Items),
            StringComparison.OrdinalIgnoreCase)
            ? AdminEntity.Items
            : AdminEntity.Spells;
    }
}
