using Npgsql;

namespace PaladinHubV2.Server.API.Services;

public sealed class NpgsqlDatabasePoolManager :
    IDatabasePoolManager
{
    public void Clear() =>
        NpgsqlConnection.ClearAllPools();
}
