using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class CategoryAdminQueryService :
    ICategoryAdminQueryService
{
    private readonly AppDbContext _db;

    public CategoryAdminQueryService(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<CategoryListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return _db.Categories
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new CategoryListItem(
                item.Id,
                item.Name,
                item.Description,
                item.ParentId,
                item.SortOrder,
                item.IsArchived,
                item.IsDeleted,
                item.Version,
                _db.Spells.Count(spell =>
                    spell.CategoryId == item.Id) +
                _db.Items.Count(product =>
                    product.CategoryId == item.Id),
                _db.Categories.Count(child =>
                    child.ParentId == item.Id &&
                    !child.IsDeleted)))
            .ToListAsync(cancellationToken);
    }

    public Task<List<CategoryRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.CategoryRevisions
            .AsNoTracking()
            .Where(revision =>
                revision.CategoryId == id)
            .OrderByDescending(revision =>
                revision.Version)
            .ToListAsync(cancellationToken);
    }
}
