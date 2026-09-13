using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Tests.Support;

internal static class PageBuilderSqliteTestDatabase
{
    public static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.CreateFunction<long, long>(
            "pg_advisory_xact_lock",
            value => value);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
