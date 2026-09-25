using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class MediaUsageCounter :
    IMediaUsageCounter
{
    private readonly AppDbContext _db;
    private readonly IMediaBannerUsageLookup _banners;

    public MediaUsageCounter(
        AppDbContext db,
        IMediaBannerUsageLookup banners)
    {
        _db = db;
        _banners = banners;
    }

    public async Task<int> CountAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        string key = id.ToString();
        string legacyPath =
            "/api/spell-icons/" + key;

        int bannerUsage =
            await _banners.CountAsync(
                id,
                cancellationToken);

        int seoUsage =
            await _db.Set<SeoEntry>().CountAsync(
                entry =>
                    !entry.IsDeleted &&
                    (entry.SocialImageMediaId == id ||
                     entry.ImageUrl.ToLower()
                         .Contains(legacyPath)),
                cancellationToken);

        int spellUsage =
            await _db.Spells.CountAsync(
                item =>
                    item.Icon != null &&
                    item.Icon.ToLower()
                        .Contains(key),
                cancellationToken);

        int itemUsage =
            await _db.Items.CountAsync(
                item =>
                    (item.Icon != null &&
                     item.Icon.ToLower()
                         .Contains(key)) ||
                    (item.SecondIcon != null &&
                     item.SecondIcon.ToLower()
                         .Contains(key)),
                cancellationToken);

        int productUsage =
            await _db.ProductImages.CountAsync(
                item =>
                    item.Url.ToLower()
                        .Contains(key),
                cancellationToken);

        int pageUsage =
            await _db.ContentPages.CountAsync(
                page =>
                    page.JsonLayout.ToLower()
                        .Contains(key),
                cancellationToken);

        int postUsage =
            await _db.DiscussionPosts.CountAsync(
                post =>
                    post.Content.ToLower()
                        .Contains(key),
                cancellationToken);

        int commentUsage =
            await _db.DiscussionComments.CountAsync(
                comment =>
                    comment.Content.ToLower()
                        .Contains(key),
                cancellationToken);

        return bannerUsage +
            seoUsage +
            spellUsage +
            itemUsage +
            productUsage +
            pageUsage +
            postUsage +
            commentUsage;
    }
}
