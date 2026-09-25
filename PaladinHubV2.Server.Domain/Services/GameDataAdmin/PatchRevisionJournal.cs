using System.Text.Json;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class PatchRevisionJournal :
    IPatchRevisionJournal
{
    private readonly AppDbContext _db;

    public PatchRevisionJournal(
        AppDbContext db)
    {
        _db = db;
    }

    public void Record(
        GamePatch patch,
        string action,
        string actor)
    {
        _db.PatchRevisions.Add(
            new PatchRevision
            {
                PatchId = patch.Id,
                Version = patch.Version,
                Action = action,
                Actor = actor,
                Snapshot =
                    JsonSerializer.Serialize(patch)
            });
    }

    public GamePatch ReadSnapshot(
        PatchRevision revision)
    {
        return JsonSerializer.Deserialize<GamePatch>(
            revision.Snapshot)!;
    }
}
