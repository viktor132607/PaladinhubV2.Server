using System.Text.Json;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class TagRevisionJournal :
    ITagRevisionJournal
{
    private readonly AppDbContext _db;

    public TagRevisionJournal(
        AppDbContext db)
    {
        _db = db;
    }

    public void Record(
        GameTag tag,
        string action,
        string actor)
    {
        _db.TagRevisions.Add(
            new TagRevision
            {
                TagId = tag.Id,
                Version = tag.Version,
                Action = action,
                Actor = actor,
                Snapshot =
                    JsonSerializer.Serialize(tag)
            });
    }

    public GameTag ReadSnapshot(
        TagRevision revision)
    {
        return JsonSerializer.Deserialize<GameTag>(
            revision.Snapshot)!;
    }
}
