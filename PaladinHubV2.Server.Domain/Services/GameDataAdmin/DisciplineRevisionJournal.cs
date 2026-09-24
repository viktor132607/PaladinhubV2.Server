using System.Text.Json;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DisciplineRevisionJournal :
    IDisciplineRevisionJournal
{
    private readonly AppDbContext _db;

    public DisciplineRevisionJournal(AppDbContext db)
    {
        _db = db;
    }

    public void Record(
        GameDiscipline discipline,
        string action,
        string actor)
    {
        _db.DisciplineRevisions.Add(
            new DisciplineRevision
            {
                DisciplineId = discipline.Id,
                Version = discipline.Version,
                Action = action,
                Actor = actor,
                Snapshot =
                    JsonSerializer.Serialize(discipline)
            });
    }

    public GameDiscipline ReadSnapshot(
        DisciplineRevision revision)
    {
        return JsonSerializer.Deserialize<GameDiscipline>(
            revision.Snapshot)!;
    }
}
