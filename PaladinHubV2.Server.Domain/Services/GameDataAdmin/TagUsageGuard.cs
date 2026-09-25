using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class TagUsageGuard :
    ITagUsageGuard
{
    private readonly AppDbContext _db;

    public TagUsageGuard(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await _db.Spells.AnyAsync(
                spell => spell.TagIds.Contains(id),
                cancellationToken) ||
            await _db.Items.AnyAsync(
                item => item.TagIds.Contains(id),
                cancellationToken);
    }
}
