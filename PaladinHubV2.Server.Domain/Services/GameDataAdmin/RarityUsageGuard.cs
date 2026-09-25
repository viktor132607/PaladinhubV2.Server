using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class RarityUsageGuard :
    IRarityUsageGuard
{
    private readonly AppDbContext _db;

    public RarityUsageGuard(
        AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.Items.AnyAsync(
            item => item.RarityId == id,
            cancellationToken);
    }
}
