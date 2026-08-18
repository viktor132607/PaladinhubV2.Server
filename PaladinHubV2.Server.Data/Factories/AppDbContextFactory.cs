using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaladinHubV2.Server.Data.Factories;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string[] environmentFileCandidates =
        [
            Path.Combine(currentDirectory, ".env"),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", ".env"))
        ];

        foreach (string environmentFile in environmentFileCandidates.Distinct())
        {
            if (File.Exists(environmentFile))
            {
                Env.Load(environmentFile);
                break;
            }
        }

        string connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=paladinhubv2db;Username=postgres;Password=postgres;";

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
