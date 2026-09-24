using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DisciplineAdminQueryService :
    IDisciplineAdminQueryService
{
    private readonly AppDbContext _db;

    public DisciplineAdminQueryService(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<DisciplineListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return _db.GameDisciplines
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new DisciplineListItem(
                item.Id,
                item.Name,
                item.Description,
                item.ParentId,
                item.SortOrder,
                item.IsArchived,
                item.IsDeleted,
                item.Version,
                _db.Spells.Count(spell =>
                    spell.DisciplineId == item.Id) +
                _db.Items.Count(product =>
                    product.DisciplineId == item.Id),
                _db.GameDisciplines.Count(child =>
                    child.ParentId == item.Id &&
                    !child.IsDeleted)))
            .ToListAsync(cancellationToken);
    }

    public Task<List<DisciplineRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.DisciplineRevisions
            .AsNoTracking()
            .Where(revision =>
                revision.DisciplineId == id)
            .OrderByDescending(revision =>
                revision.Version)
            .ToListAsync(cancellationToken);
    }
}
