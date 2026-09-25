using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class CategoryUsageGuard :
    ICategoryUsageGuard
{
    private readonly AppDbContext _db;

    public CategoryUsageGuard(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.Categories.AnyAsync(
                item =>
                    item.ParentId == id &&
                    !item.IsDeleted,
                cancellationToken) ||
            await _db.Spells.AnyAsync(
                spell => spell.CategoryId == id,
                cancellationToken) ||
            await _db.Items.AnyAsync(
                item => item.CategoryId == id,
                cancellationToken);
    }
}
