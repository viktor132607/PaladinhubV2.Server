using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class MediaBannerUsageLookup :
    IMediaBannerUsageLookup
{
    private readonly AppDbContext _db;

    public MediaBannerUsageLookup(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CountAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        string? provider =
            _db.Database.ProviderName;

        if (provider?.Contains(
                "Npgsql",
                StringComparison.OrdinalIgnoreCase) != true)
        {
            return 0;
        }

        return await _db.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::integer AS "Value"
                FROM "SiteBanners"
                WHERE NOT "IsDeleted"
                  AND "ImageUrl" ILIKE {'%' + id.ToString() + '%'}
                """)
            .SingleAsync(cancellationToken);
    }
}
