using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class MediaAdminQueryService :
    IMediaAdminQueryService
{
    private readonly AppDbContext _db;
    private readonly IMediaUsageCounter _usage;

    public MediaAdminQueryService(
        AppDbContext db,
        IMediaUsageCounter usage)
    {
        _db = db;
        _usage = usage;
    }

    public async Task<MediaPageResult> ListAsync(
        string? search,
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<SpellIcon> query =
            _db.SpellIcons.AsNoTracking();

        query = status switch
        {
            "all" => query,
            "deleted" =>
                query.Where(item =>
                    item.IsDeleted),
            "archived" =>
                query.Where(item =>
                    !item.IsDeleted &&
                    item.IsArchived),
            _ =>
                query.Where(item =>
                    !item.IsDeleted &&
                    !item.IsArchived)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            string text =
                search.Trim().ToLower();

            query = query.Where(item =>
                item.Name.ToLower().Contains(text) ||
                item.AltText.ToLower().Contains(text) ||
                item.Description.ToLower().Contains(text));
        }

        pageSize = Math.Clamp(
            pageSize,
            1,
            100);

        int total =
            await query.CountAsync(
                cancellationToken);

        int pages = Math.Max(
            1,
            (int)Math.Ceiling(
                total / (double)pageSize));

        page = Math.Clamp(
            page,
            1,
            pages);

        List<MediaListItem> media =
            await query
                .OrderByDescending(item =>
                    item.CreatedAtUtc)
                .ThenBy(item =>
                    item.Id)
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(pageSize)
                .Select(item =>
                    new MediaListItem(
                        item.Id,
                        item.Name,
                        item.AltText,
                        item.Description,
                        item.IsArchived,
                        item.IsDeleted,
                        item.Version,
                        item.CreatedAtUtc,
                        item.ContentType,
                        item.Content.Length,
                        "/api/spell-icons/" +
                        item.Id,
                        0))
                .ToListAsync(
                    cancellationToken);

        for (int index = 0;
             index < media.Count;
             index++)
        {
            MediaListItem item =
                media[index];

            int usageCount =
                await _usage.CountAsync(
                    item.Id,
                    cancellationToken);

            media[index] =
                item with
                {
                    UsageCount = usageCount
                };
        }

        return new MediaPageResult(
            media,
            page,
            pages,
            total);
    }

    public Task<List<MediaRevision>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return _db.MediaRevisions
            .AsNoTracking()
            .Where(revision =>
                revision.MediaId == id)
            .OrderByDescending(revision =>
                revision.Version)
            .ToListAsync(
                cancellationToken);
    }
}
