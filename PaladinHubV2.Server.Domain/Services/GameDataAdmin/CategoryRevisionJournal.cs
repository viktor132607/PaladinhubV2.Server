using System.Text.Json;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class CategoryRevisionJournal :
    ICategoryRevisionJournal
{
    private readonly AppDbContext _db;

    public CategoryRevisionJournal(AppDbContext db)
    {
        _db = db;
    }

    public void Record(
        Category category,
        string action,
        string actor)
    {
        _db.CategoryRevisions.Add(
            new CategoryRevision
            {
                CategoryId = category.Id,
                Version = category.Version,
                Action = action,
                Actor = actor,
                Snapshot =
                    JsonSerializer.Serialize(category)
            });
    }

    public Category ReadSnapshot(
        CategoryRevision revision)
    {
        return JsonSerializer.Deserialize<Category>(
            revision.Snapshot)!;
    }
}
