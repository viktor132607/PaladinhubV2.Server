using Microsoft.EntityFrameworkCore;

namespace PaladinHubV2.Data.Helpers;

public static class DatabaseUtils
{
    public static async Task TruncateAllTablesSafeAsync(DbContext context)
    {
        List<string> tableNames = await context.Database
            .SqlQueryRaw<string>(
                @"SELECT tablename FROM pg_tables WHERE schemaname = 'public'")
            .ToListAsync();

        foreach (string table in tableNames)
        {
            await context.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS \"{table}\" CASCADE;");
        }
    }
}
