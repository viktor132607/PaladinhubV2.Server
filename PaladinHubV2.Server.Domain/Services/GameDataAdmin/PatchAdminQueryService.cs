using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class PatchAdminQueryService :
    IPatchAdminQueryService
{
    private readonly AppDbContext _db;

    public PatchAdminQueryService(
        AppDbContext db)
    {
        _db = db;
    }

    public Task<List<PatchListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return _db.GamePatches
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new PatchListItem(
                item.Id,
                item.Name,
                item.Description,
                null,
                item.SortOrder,
                item.IsArchived,
                item.IsDeleted,
                item.Version,
                _db.Spells.Count(spell =>
                    spell.PatchId == item.Id) +
                _db.Items.Count(product =>
                    product.PatchId == item.Id),
                0))
            .ToListAsync(cancellationToken);
    }

    public Task<List<PatchRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.PatchRevisions
            .AsNoTracking()
            .Where(revision =>
                revision.PatchId == id)
            .OrderByDescending(revision =>
                revision.Version)
            .ToListAsync(cancellationToken);
    }
}
