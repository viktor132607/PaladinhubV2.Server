using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.API.Controllers.GameData;

internal static class CategoryRules
{
    // Serializes catalog edits and record assignment to prevent cycles and assignment/delete races.
    public static async Task<IDbContextTransaction> BeginAsync(AppDbContext db, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try { await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(8820411)", ct); }
        catch { await transaction.DisposeAsync(); throw; }
        return transaction;
    }

    public static async Task<bool> CanAssignAsync(AppDbContext db, int? id, int? previousId, CancellationToken ct)
    {
        if (id is null) return true;
        return await db.Categories.AnyAsync(category => category.Id == id && !category.IsDeleted &&
            (!category.IsArchived || id == previousId), ct);
    }
}
