using System.Text.Json;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class MediaRevisionJournal :
    IMediaRevisionJournal
{
    private readonly AppDbContext _db;

    public MediaRevisionJournal(
        AppDbContext db)
    {
        _db = db;
    }

    public void Record(
        SpellIcon media,
        string action,
        string actor)
    {
        _db.MediaRevisions.Add(
            new MediaRevision
            {
                MediaId = media.Id,
                Version = media.Version,
                Action = action,
                Actor = actor,
                Snapshot =
                    JsonSerializer.Serialize(
                        new MediaSnapshot(
                            media.Name,
                            media.AltText,
                            media.Description,
                            media.IsArchived,
                            media.IsDeleted))
            });
    }

    public MediaSnapshot? ReadSnapshot(
        MediaRevision revision)
    {
        return JsonSerializer
            .Deserialize<MediaSnapshot>(
                revision.Snapshot);
    }
}
