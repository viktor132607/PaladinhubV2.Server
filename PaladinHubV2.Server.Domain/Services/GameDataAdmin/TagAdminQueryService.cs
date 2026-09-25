using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class TagAdminQueryService :
    ITagAdminQueryService
{
    private readonly AppDbContext _db;

    public TagAdminQueryService(
        AppDbContext db)
    {
        _db = db;
    }

    public Task<List<TagListItem>> ListAsync(
        CancellationToken cancellationToken)
    {
        return _db.GameTags
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new TagListItem(
                item.Id,
                item.Name,
                item.Description,
                null,
                item.SortOrder,
                item.IsArchived,
                item.IsDeleted,
                item.Version,
                _db.Spells.Count(spell =>
                    spell.TagIds.Contains(item.Id)) +
                _db.Items.Count(product =>
                    product.TagIds.Contains(item.Id)),
                0))
            .ToListAsync(cancellationToken);
    }

    public Task<List<TagRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.TagRevisions
            .AsNoTracking()
            .Where(revision =>
                revision.TagId == id)
            .OrderByDescending(revision =>
                revision.Version)
            .ToListAsync(cancellationToken);
    }
}
