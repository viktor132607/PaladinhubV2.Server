using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Banners;

public sealed class PostgresBannerRepository : IBannerRepository
{
    private readonly AppDbContext _db;

    public PostgresBannerRepository(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<BannerDto>> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection =
            await ConnectionAsync(
                cancellationToken);

        await using var command =
            new NpgsqlCommand(
                """
                SELECT *
                FROM "SiteBanners"
                ORDER BY
                    "Position",
                    "SortOrder",
                    "InternalName"
                """,
                connection);

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        var result = new List<BannerDto>();

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            result.Add(Read(reader));
        }

        return result;
    }

    public async Task<List<BannerRevisionDto>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection =
            await ConnectionAsync(
                cancellationToken);

        await using var command =
            new NpgsqlCommand(
                """
                SELECT
                    "Id",
                    "BannerId",
                    "Version",
                    "Action",
                    "Actor",
                    "CreatedAtUtc",
                    "Snapshot"::text
                FROM "BannerRevisions"
                WHERE "BannerId" = @id
                ORDER BY "Version" DESC
                """,
                connection);

        command.Parameters.AddWithValue(
            "id",
            id);

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        var result =
            new List<BannerRevisionDto>();

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            result.Add(
                new BannerRevisionDto(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetFieldValue<DateTimeOffset>(5),
                    JsonSerializer.Deserialize<BannerDto>(
                        reader.GetString(6))!));
        }

        return result;
    }

    public async Task<BannerDto?> FindAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection =
            await ConnectionAsync(
                cancellationToken);

        await using var command =
            new NpgsqlCommand(
                """
                SELECT *
                FROM "SiteBanners"
                WHERE "Id" = @id
                """,
                connection);

        command.Parameters.AddWithValue(
            "id",
            id);

        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        return await reader.ReadAsync(
            cancellationToken)
            ? Read(reader)
            : null;
    }

    public async Task<BannerDto?> RevisionSnapshotAsync(
        Guid bannerId,
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection =
            await ConnectionAsync(
                cancellationToken);

        await using var command =
            new NpgsqlCommand(
                """
                SELECT "Snapshot"::text
                FROM "BannerRevisions"
                WHERE "BannerId" = @banner
                  AND "Id" = @id
                """,
                connection);

        command.Parameters.AddWithValue(
            "banner",
            bannerId);

        command.Parameters.AddWithValue(
            "id",
            revisionId);

        string? value =
            await command.ExecuteScalarAsync(
                cancellationToken)
            as string;

        return value is null
            ? null
            : JsonSerializer.Deserialize<BannerDto>(
                value);
    }

    public async Task<BannerStoreResult> CreateAsync(
        BannerDto banner,
        string actor,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection =
            await ConnectionAsync(
                cancellationToken);

        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(
                cancellationToken);

        try
        {
            BannerStoreResult? mediaError =
                await LockAndValidateMediaAsync(
                    connection,
                    transaction,
                    banner,
                    null,
                    cancellationToken);

            if (mediaError is not null)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return mediaError;
            }

            await InsertAsync(
                connection,
                transaction,
                banner,
                cancellationToken);

            await RevisionAsync(
                connection,
                transaction,
                banner,
                "created",
                actor,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new BannerStoreResult(
                200,
                "ok",
                string.Empty,
                banner);
        }
        catch (PostgresException exception)
            when (exception.SqlState ==
                  PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return Duplicate();
        }
    }

    public async Task<BannerStoreResult> ReplaceAsync(
        BannerDto current,
        BannerDto next,
        string action,
        string actor,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection =
            await ConnectionAsync(
                cancellationToken);

        await using NpgsqlTransaction transaction =
            await connection.BeginTransactionAsync(
                cancellationToken);

        try
        {
            BannerStoreResult? mediaError =
                await LockAndValidateMediaAsync(
                    connection,
                    transaction,
                    next,
                    current.IsDeleted
                        ? null
                        : current.ImageUrl,
                    cancellationToken);

            if (mediaError is not null)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return mediaError;
            }

            await using var command =
                new NpgsqlCommand(
                    """
                    UPDATE "SiteBanners"
                    SET
                        "InternalName" = @name,
                        "Title" = @title,
                        "Text" = @text,
                        "ImageUrl" = @image,
                        "AltText" = @alt,
                        "ButtonText" = @buttonText,
                        "ButtonUrl" = @buttonUrl,
                        "Kind" = @kind,
                        "Position" = @position,
                        "PagesJson" = CAST(@pages AS jsonb),
                        "StartAtUtc" = @start,
                        "EndAtUtc" = @end,
                        "SortOrder" = @sort,
                        "IsDismissible" = @dismiss,
                        "IsActive" = @active,
                        "IsArchived" = @archived,
                        "IsDeleted" = @deleted,
                        "Version" = @nextVersion,
                        "UpdatedAtUtc" = @updated
                    WHERE "Id" = @id
                      AND "Version" = @version
                    """,
                    connection,
                    transaction);

            Bind(command, next);

            command.Parameters.AddWithValue(
                "id",
                current.Id);

            command.Parameters.AddWithValue(
                "version",
                current.Version);

            if (await command.ExecuteNonQueryAsync(
                    cancellationToken) == 0)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return new BannerStoreResult(
                    409,
                    "banner.stale",
                    "The banner was changed by another user. Reload before continuing.");
            }

            await RevisionAsync(
                connection,
                transaction,
                next,
                action,
                actor,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new BannerStoreResult(
                200,
                "ok",
                string.Empty,
                next);
        }
        catch (PostgresException exception)
            when (exception.SqlState ==
                  PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return Duplicate();
        }
    }

    private static BannerStoreResult Duplicate()
    {
        return new BannerStoreResult(
            409,
            "banner.duplicate",
            "A banner with that internal name already exists.");
    }

    private async Task<BannerStoreResult?>
        LockAndValidateMediaAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            BannerDto next,
            string? previous,
            CancellationToken cancellationToken)
    {
        await using (var mutex =
                     new NpgsqlCommand(
                         "SELECT pg_advisory_xact_lock(8820411)",
                         connection,
                         transaction))
        {
            await mutex.ExecuteNonQueryAsync(
                cancellationToken);
        }

        if (next.IsDeleted ||
            next.ImageUrl == previous ||
            string.IsNullOrWhiteSpace(
                next.ImageUrl))
        {
            return null;
        }

        System.Text.RegularExpressions.Match match =
            System.Text.RegularExpressions.Regex.Match(
                next.ImageUrl,
                @"/(?:spell-icons|icons)/([0-9a-fA-F-]{36})(?:$|[/?#])");

        if (!match.Success ||
            !Guid.TryParse(
                match.Groups[1].Value,
                out Guid id))
        {
            return null;
        }

        await using var media =
            new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM "SpellIcons"
                WHERE "Id" = @id
                  AND NOT "IsDeleted"
                  AND NOT "IsArchived"
                """,
                connection,
                transaction);

        media.Parameters.AddWithValue(
            "id",
            id);

        int count = Convert.ToInt32(
            await media.ExecuteScalarAsync(
                cancellationToken));

        return count > 0
            ? null
            : new BannerStoreResult(
                409,
                "banner.mediaUnavailable",
                "Choose an active image from the media library.");
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BannerDto banner,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                INSERT INTO "SiteBanners"(
                    "Id",
                    "InternalName",
                    "Title",
                    "Text",
                    "ImageUrl",
                    "AltText",
                    "ButtonText",
                    "ButtonUrl",
                    "Kind",
                    "Position",
                    "PagesJson",
                    "StartAtUtc",
                    "EndAtUtc",
                    "SortOrder",
                    "IsDismissible",
                    "IsActive",
                    "IsArchived",
                    "IsDeleted",
                    "Version",
                    "CreatedAtUtc",
                    "UpdatedAtUtc")
                VALUES(
                    @id,
                    @name,
                    @title,
                    @text,
                    @image,
                    @alt,
                    @buttonText,
                    @buttonUrl,
                    @kind,
                    @position,
                    CAST(@pages AS jsonb),
                    @start,
                    @end,
                    @sort,
                    @dismiss,
                    @active,
                    @archived,
                    @deleted,
                    @nextVersion,
                    @created,
                    @updated)
                """,
                connection,
                transaction);

        Bind(command, banner);

        command.Parameters.AddWithValue(
            "id",
            banner.Id);

        command.Parameters.AddWithValue(
            "created",
            banner.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static void Bind(
        NpgsqlCommand command,
        BannerDto banner)
    {
        command.Parameters.AddWithValue(
            "name",
            banner.InternalName);

        command.Parameters.AddWithValue(
            "title",
            banner.Title);

        command.Parameters.AddWithValue(
            "text",
            banner.Text);

        command.Parameters.Add(
            new NpgsqlParameter(
                "image",
                NpgsqlDbType.Varchar)
            {
                Value =
                    (object?)banner.ImageUrl ??
                    DBNull.Value
            });

        command.Parameters.AddWithValue(
            "alt",
            banner.AltText);

        command.Parameters.Add(
            new NpgsqlParameter(
                "buttonText",
                NpgsqlDbType.Varchar)
            {
                Value =
                    (object?)banner.ButtonText ??
                    DBNull.Value
            });

        command.Parameters.Add(
            new NpgsqlParameter(
                "buttonUrl",
                NpgsqlDbType.Varchar)
            {
                Value =
                    (object?)banner.ButtonUrl ??
                    DBNull.Value
            });

        command.Parameters.AddWithValue(
            "kind",
            banner.Kind);

        command.Parameters.AddWithValue(
            "position",
            banner.Position);

        command.Parameters.AddWithValue(
            "pages",
            JsonSerializer.Serialize(
                banner.Pages));

        command.Parameters.Add(
            new NpgsqlParameter(
                "start",
                NpgsqlDbType.TimestampTz)
            {
                Value =
                    (object?)banner.StartAtUtc ??
                    DBNull.Value
            });

        command.Parameters.Add(
            new NpgsqlParameter(
                "end",
                NpgsqlDbType.TimestampTz)
            {
                Value =
                    (object?)banner.EndAtUtc ??
                    DBNull.Value
            });

        command.Parameters.AddWithValue(
            "sort",
            banner.SortOrder);

        command.Parameters.AddWithValue(
            "dismiss",
            banner.IsDismissible);

        command.Parameters.AddWithValue(
            "active",
            banner.IsActive);

        command.Parameters.AddWithValue(
            "archived",
            banner.IsArchived);

        command.Parameters.AddWithValue(
            "deleted",
            banner.IsDeleted);

        command.Parameters.AddWithValue(
            "nextVersion",
            banner.Version);

        command.Parameters.AddWithValue(
            "updated",
            banner.UpdatedAtUtc);
    }

    private static async Task RevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BannerDto banner,
        string action,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                INSERT INTO "BannerRevisions"(
                    "Id",
                    "BannerId",
                    "Version",
                    "Action",
                    "Actor",
                    "CreatedAtUtc",
                    "Snapshot")
                VALUES(
                    @id,
                    @banner,
                    @version,
                    @action,
                    @actor,
                    @created,
                    CAST(@snapshot AS jsonb))
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "id",
            Guid.NewGuid());

        command.Parameters.AddWithValue(
            "banner",
            banner.Id);

        command.Parameters.AddWithValue(
            "version",
            banner.Version);

        command.Parameters.AddWithValue(
            "action",
            action);

        command.Parameters.AddWithValue(
            "actor",
            actor);

        command.Parameters.AddWithValue(
            "created",
            DateTimeOffset.UtcNow);

        command.Parameters.AddWithValue(
            "snapshot",
            JsonSerializer.Serialize(
                banner));

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    private static BannerDto Read(
        NpgsqlDataReader reader)
    {
        DateTimeOffset? Time(string name) =>
            reader.IsDBNull(
                reader.GetOrdinal(name))
                ? null
                : reader.GetFieldValue<DateTimeOffset>(
                    reader.GetOrdinal(name));

        string? Text(string name) =>
            reader.IsDBNull(
                reader.GetOrdinal(name))
                ? null
                : reader.GetString(
                    reader.GetOrdinal(name));

        IReadOnlyList<string> pages =
            JsonSerializer.Deserialize<List<string>>(
                reader.GetString(
                    reader.GetOrdinal(
                        "PagesJson"))) ??
            [];

        return new BannerDto(
            reader.GetGuid(
                reader.GetOrdinal("Id")),
            reader.GetString(
                reader.GetOrdinal("InternalName")),
            reader.GetString(
                reader.GetOrdinal("Title")),
            reader.GetString(
                reader.GetOrdinal("Text")),
            Text("ImageUrl"),
            reader.GetString(
                reader.GetOrdinal("AltText")),
            Text("ButtonText"),
            Text("ButtonUrl"),
            reader.GetString(
                reader.GetOrdinal("Kind")),
            reader.GetString(
                reader.GetOrdinal("Position")),
            pages,
            Time("StartAtUtc"),
            Time("EndAtUtc"),
            reader.GetInt32(
                reader.GetOrdinal("SortOrder")),
            reader.GetBoolean(
                reader.GetOrdinal("IsDismissible")),
            reader.GetBoolean(
                reader.GetOrdinal("IsActive")),
            reader.GetBoolean(
                reader.GetOrdinal("IsArchived")),
            reader.GetBoolean(
                reader.GetOrdinal("IsDeleted")),
            reader.GetInt32(
                reader.GetOrdinal("Version")),
            reader.GetFieldValue<DateTimeOffset>(
                reader.GetOrdinal("CreatedAtUtc")),
            reader.GetFieldValue<DateTimeOffset>(
                reader.GetOrdinal("UpdatedAtUtc")));
    }

    private async Task<NpgsqlConnection>
        ConnectionAsync(
            CancellationToken cancellationToken)
    {
        var connection =
            (NpgsqlConnection)_db.Database
                .GetDbConnection();

        if (connection.State !=
            ConnectionState.Open)
        {
            await connection.OpenAsync(
                cancellationToken);
        }

        return connection;
    }
}
