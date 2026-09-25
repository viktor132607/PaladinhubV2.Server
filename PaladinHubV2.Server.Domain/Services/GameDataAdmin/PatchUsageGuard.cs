using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class PatchUsageGuard :
    IPatchUsageGuard
{
    private readonly AppDbContext _db;

    public PatchUsageGuard(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.Spells.AnyAsync(
                spell => spell.PatchId == id,
                cancellationToken) ||
            await _db.Items.AnyAsync(
                item => item.PatchId == id,
                cancellationToken);
    }
}
