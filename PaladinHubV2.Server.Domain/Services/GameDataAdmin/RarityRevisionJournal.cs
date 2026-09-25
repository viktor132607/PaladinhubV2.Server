using System.Text.Json;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class RarityRevisionJournal :
    IRarityRevisionJournal
{
    private readonly AppDbContext _db;

    public RarityRevisionJournal(
        AppDbContext db)
    {
        _db = db;
    }

    public void Record(
        ItemRarity rarity,
        string action,
        string actor)
    {
        _db.RarityRevisions.Add(
            new RarityRevision
            {
                RarityId = rarity.Id,
                Version = rarity.Version,
                Action = action,
                Actor = actor,
                Snapshot =
                    JsonSerializer.Serialize(rarity)
            });
    }

    public ItemRarity ReadSnapshot(
        RarityRevision revision)
    {
        return JsonSerializer.Deserialize<ItemRarity>(
            revision.Snapshot)!;
    }
}
