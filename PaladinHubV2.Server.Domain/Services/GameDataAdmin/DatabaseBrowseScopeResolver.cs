using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DatabaseBrowseScopeResolver :
    IDatabaseBrowseScopeResolver
{
    private readonly AppDbContext _db;

    public DatabaseBrowseScopeResolver(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<HashSet<int>> ResolveCategoryIdsAsync(
        int? categoryId,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<int>();

        if (categoryId is not > 0)
        {
            return result;
        }

        var categories =
            await _db.Categories
                .AsNoTracking()
                .Where(category =>
                    !category.IsDeleted)
                .Select(category =>
                    new
                    {
                        category.Id,
                        category.ParentId
                    })
                .ToListAsync(
                    cancellationToken);

        if (!categories.Any(category =>
                category.Id ==
                categoryId.Value))
        {
            return result;
        }

        result.Add(categoryId.Value);

        var queue = new Queue<int>();
        queue.Enqueue(categoryId.Value);

        while (queue.TryDequeue(
                   out int parentId))
        {
            foreach (var child in
                     categories.Where(category =>
                         category.ParentId ==
                         parentId))
            {
                if (result.Add(child.Id))
                {
                    queue.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    public async Task<List<int>?> ResolveDisciplineIdsAsync(
        int? disciplineId,
        CancellationToken cancellationToken)
    {
        if (disciplineId is not > 0)
        {
            return [];
        }

        bool exists =
            await _db.GameDisciplines.AnyAsync(
                discipline =>
                    discipline.Id ==
                    disciplineId.Value &&
                    !discipline.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            return null;
        }

        return await _db.GameDisciplines
            .Where(discipline =>
                !discipline.IsDeleted &&
                (discipline.Id ==
                     disciplineId.Value ||
                 discipline.ParentId ==
                     disciplineId.Value))
            .Select(discipline =>
                discipline.Id)
            .ToListAsync(
                cancellationToken);
    }
}
