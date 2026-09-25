using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DatabaseItemBrowseQuery :
    IDatabaseItemBrowseQuery
{
    private readonly AppDbContext _db;

    public DatabaseItemBrowseQuery(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task PopulateAsync(
        AdminDatabaseIndexViewModel model,
        DatabaseBrowseFilters filters,
        CancellationToken cancellationToken)
    {
        var query =
            _db.Items
                .AsNoTracking()
                .AsQueryable();

        if (filters.CategoryId == 0)
        {
            query = query.Where(item =>
                item.CategoryId == null);
        }
        else if (filters.CategoryId > 0)
        {
            query = query.Where(item =>
                item.CategoryId != null &&
                filters.CategoryIds.Contains(
                    item.CategoryId.Value));
        }

        if (filters.DisciplineId == 0)
        {
            query = query.Where(item =>
                item.DisciplineId == null);
        }
        else if (filters.DisciplineId > 0)
        {
            query = query.Where(item =>
                item.DisciplineId != null &&
                filters.DisciplineIds.Contains(
                    item.DisciplineId.Value));
        }

        if (filters.RarityId == 0)
        {
            query = query.Where(item =>
                item.RarityId == null);
        }
        else if (filters.RarityId > 0)
        {
            query = query.Where(item =>
                item.RarityId ==
                filters.RarityId);
        }

        if (filters.PatchId == 0)
        {
            query = query.Where(item =>
                item.PatchId == null);
        }
        else if (filters.PatchId > 0)
        {
            query = query.Where(item =>
                item.PatchId ==
                filters.PatchId);
        }

        if (filters.TagId == 0)
        {
            query = query.Where(item =>
                item.TagIds.Length == 0);
        }
        else if (filters.TagId > 0)
        {
            query = query.Where(item =>
                item.TagIds.Contains(
                    filters.TagId.Value));
        }

        if (!string.IsNullOrWhiteSpace(
                filters.Search))
        {
            query = query.Where(item =>
                item.Name.Contains(
                    filters.Search) ||
                _db.GameTags.Any(tag =>
                    !tag.IsDeleted &&
                    item.TagIds.Contains(tag.Id) &&
                    tag.Name.Contains(
                        filters.Search)) ||
                (item.Description ??
                    string.Empty).Contains(
                    filters.Search));
        }

        model.Total =
            await query.CountAsync(
                cancellationToken);

        DatabaseBrowserService.ClampPage(
            model);

        model.Items =
            await query
                .OrderBy(item =>
                    item.Name)
                .Skip(
                    (model.Page - 1) *
                    model.PageSize)
                .Take(model.PageSize)
                .ToListAsync(
                    cancellationToken);
    }
}
