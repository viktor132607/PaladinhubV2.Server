using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class RarityAdminQueryService :
    IRarityAdminQueryService
{
    private readonly AppDbContext _db;

    public RarityAdminQueryService(
        AppDbContext db)
    {
        _db = db;
    }

    public Task<List<RarityListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return _db.ItemRarities
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new RarityListItem(
                item.Id,
                item.Name,
                item.Description,
                item.Color,
                null,
                item.SortOrder,
                item.IsArchived,
                item.IsDeleted,
                item.Version,
                _db.Items.Count(product =>
                    product.RarityId == item.Id),
                0))
            .ToListAsync(cancellationToken);
    }

    public Task<List<RarityRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.RarityRevisions
            .AsNoTracking()
            .Where(revision =>
                revision.RarityId == id)
            .OrderByDescending(revision =>
                revision.Version)
            .ToListAsync(cancellationToken);
    }
}
