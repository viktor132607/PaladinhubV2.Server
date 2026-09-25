using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class RarityQualitySynchronizer :
    IRarityQualitySynchronizer
{
    private readonly AppDbContext _db;

    public RarityQualitySynchronizer(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task SyncAsync(
        ItemRarity rarity,
        CancellationToken cancellationToken)
    {
        await _db.Items
            .Where(item =>
                item.RarityId == rarity.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Quality,
                    rarity.Name),
                cancellationToken);
    }
}
