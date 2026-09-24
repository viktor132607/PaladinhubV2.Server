using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DisciplineUsageGuard :
    IDisciplineUsageGuard
{
    private readonly AppDbContext _db;

    public DisciplineUsageGuard(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.GameDisciplines.AnyAsync(
                item =>
                    item.ParentId == id &&
                    !item.IsDeleted,
                cancellationToken) ||
            await _db.Spells.AnyAsync(
                spell => spell.DisciplineId == id,
                cancellationToken) ||
            await _db.Items.AnyAsync(
                item => item.DisciplineId == id,
                cancellationToken);
    }
}
