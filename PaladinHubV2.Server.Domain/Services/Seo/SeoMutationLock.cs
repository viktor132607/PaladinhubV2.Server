using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Seo;

public interface ISeoMutationLock
{
    Task AcquireAsync(CancellationToken ct);
}

public sealed class SeoMutationLock : ISeoMutationLock
{
    private const long MutationLockKey = 8820414;
    private readonly AppDbContext _db;

    public SeoMutationLock(AppDbContext db)
    {
        _db = db;
    }

    public async Task AcquireAsync(CancellationToken ct)
    {
        string? provider = _db.Database.ProviderName;
        if (provider?.Contains(
                "Npgsql",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_xact_lock({MutationLockKey})",
                ct);
        }
    }
}
