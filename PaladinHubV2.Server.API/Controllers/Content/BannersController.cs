using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.API.Controllers.Content;

public sealed record BannerRequest(
    string InternalName,
    string Title,
    string Text,
    string? ImageUrl,
    string? AltText,
    string? ButtonText,
    string? ButtonUrl,
    string Kind,
    string Position,
    IReadOnlyList<string>? Pages,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    int SortOrder,
    bool IsDismissible,
    bool IsActive,
    int Version);

public sealed record BannerActionRequest(int Version, string Action, Guid? RevisionId);

public sealed record BannerDto(
    Guid Id,
    string InternalName,
    string Title,
    string Text,
    string? ImageUrl,
    string AltText,
    string? ButtonText,
    string? ButtonUrl,
    string Kind,
    string Position,
    IReadOnlyList<string> Pages,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    int SortOrder,
    bool IsDismissible,
    bool IsActive,
    bool IsArchived,
    bool IsDeleted,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record BannerRevisionDto(
    Guid Id,
    Guid BannerId,
    int Version,
    string Action,
    string Actor,
    DateTimeOffset CreatedAtUtc,
    BannerDto Snapshot);

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/banners")]
public sealed class BannersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string status = "active", CancellationToken ct = default)
    {
        await BannerStore.EnsureSchemaAsync(db, ct);
        var items = await BannerStore.ReadAllAsync(db, ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            items = items.Where(x => x.InternalName.Contains(needle, StringComparison.OrdinalIgnoreCase) || x.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) || x.Text.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        items = status.ToLowerInvariant() switch
        {
            "all" => items,
            "archived" => items.Where(x => x.IsArchived && !x.IsDeleted).ToList(),
            "deleted" => items.Where(x => x.IsDeleted).ToList(),
            "inactive" => items.Where(x => !x.IsDeleted && !x.IsArchived && !x.IsActive).ToList(),
            _ => items.Where(x => !x.IsDeleted && !x.IsArchived && x.IsActive).ToList()
        };
        return Ok(items.OrderBy(x => x.Position).ThenBy(x => x.SortOrder).ThenBy(x => x.InternalName));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        await BannerStore.EnsureSchemaAsync(db, ct);
        return Ok(await BannerStore.HistoryAsync(db, id, ct));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BannerRequest request, CancellationToken ct)
    {
        var validation = BannerStore.Validate(request, creating: true);
        if (validation is not null) return BadRequest(new { code = "banner.validation", message = validation });
        await BannerStore.EnsureSchemaAsync(db, ct);
        var item = await BannerStore.CreateAsync(db, request, User.Identity?.Name ?? "admin", ct);
        return Created($"/Admin/api/banners/{item.Id}", item);
    }

    [HttpPut("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, BannerRequest request, CancellationToken ct)
    {
        var validation = BannerStore.Validate(request, creating: false);
        if (validation is not null) return BadRequest(new { code = "banner.validation", message = validation });
        await BannerStore.EnsureSchemaAsync(db, ct);
        var result = await BannerStore.UpdateAsync(db, id, request, User.Identity?.Name ?? "admin", ct);
        return Result(result);
    }

    [HttpPost("{id:guid}/actions"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(Guid id, BannerActionRequest request, CancellationToken ct)
    {
        await BannerStore.EnsureSchemaAsync(db, ct);
        var result = await BannerStore.ChangeAsync(db, id, request, User.Identity?.Name ?? "admin", ct);
        return Result(result);
    }

    private IActionResult Result(BannerStoreResult result) => result.Status switch
    {
        200 when result.Banner is not null => Ok(result.Banner),
        404 => NotFound(new { code = result.Code, message = result.Message }),
        409 => Conflict(new { code = result.Code, message = result.Message }),
        _ => BadRequest(new { code = result.Code, message = result.Message })
    };
}

[ApiController, Route("api/banners")]
public sealed class PublicBannersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Visible([FromQuery] string path = "/", CancellationToken ct = default)
    {
        await BannerStore.EnsureSchemaAsync(db, ct);
        path = BannerStore.NormalizePath(path);
        var now = DateTimeOffset.UtcNow;
        var visible = (await BannerStore.ReadAllAsync(db, ct))
            .Where(x => !x.IsDeleted && !x.IsArchived && x.IsActive)
            .Where(x => x.StartAtUtc is null || x.StartAtUtc <= now)
            .Where(x => x.EndAtUtc is null || x.EndAtUtc >= now)
            .Where(x => x.Pages.Count == 0 || x.Pages.Any(p => string.Equals(BannerStore.NormalizePath(p), path, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Position)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id, x.Title, x.Text, x.ImageUrl, x.AltText, x.ButtonText, x.ButtonUrl,
                x.Kind, x.Position, x.StartAtUtc, x.EndAtUtc, x.SortOrder, x.IsDismissible, x.Version,
                translationKeys = new
                {
                    title = $"banner.{x.Id:N}.title",
                    text = $"banner.{x.Id:N}.text",
                    alt = $"banner.{x.Id:N}.alt",
                    button = $"banner.{x.Id:N}.button"
                }
            });
        return Ok(visible);
    }
}

internal sealed record BannerStoreResult(int Status, string Code, string Message, BannerDto? Banner = null);

internal static class BannerStore
{
    private const string Schema = """
CREATE TABLE IF NOT EXISTS "SiteBanners" (
    "Id" uuid PRIMARY KEY,
    "InternalName" varchar(100) NOT NULL,
    "Title" varchar(200) NOT NULL,
    "Text" text NOT NULL,
    "ImageUrl" varchar(2048) NULL,
    "AltText" varchar(300) NOT NULL DEFAULT '',
    "ButtonText" varchar(120) NULL,
    "ButtonUrl" varchar(2048) NULL,
    "Kind" varchar(16) NOT NULL,
    "Position" varchar(32) NOT NULL,
    "PagesJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
    "StartAtUtc" timestamptz NULL,
    "EndAtUtc" timestamptz NULL,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "IsDismissible" boolean NOT NULL DEFAULT true,
    "IsActive" boolean NOT NULL DEFAULT true,
    "IsArchived" boolean NOT NULL DEFAULT false,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "Version" integer NOT NULL DEFAULT 1,
    "CreatedAtUtc" timestamptz NOT NULL,
    "UpdatedAtUtc" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SiteBanners_InternalName" ON "SiteBanners" (lower("InternalName")) WHERE NOT "IsDeleted";
CREATE INDEX IF NOT EXISTS "IX_SiteBanners_Visibility" ON "SiteBanners" ("IsDeleted", "IsArchived", "IsActive", "StartAtUtc", "EndAtUtc", "Position", "SortOrder");
CREATE TABLE IF NOT EXISTS "BannerRevisions" (
    "Id" uuid PRIMARY KEY,
    "BannerId" uuid NOT NULL REFERENCES "SiteBanners"("Id") ON DELETE RESTRICT,
    "Version" integer NOT NULL,
    "Action" varchar(32) NOT NULL,
    "Actor" varchar(256) NOT NULL,
    "CreatedAtUtc" timestamptz NOT NULL,
    "Snapshot" jsonb NOT NULL,
    CONSTRAINT "UQ_BannerRevisions_Banner_Version" UNIQUE ("BannerId", "Version")
);
CREATE OR REPLACE FUNCTION ph_protect_banner_media() RETURNS trigger AS $$
BEGIN
    IF NEW."IsDeleted" AND NOT OLD."IsDeleted" AND to_regclass('\"SiteBanners\"') IS NOT NULL AND EXISTS (
        SELECT 1 FROM "SiteBanners" b
        WHERE NOT b."IsDeleted" AND b."ImageUrl" IS NOT NULL AND b."ImageUrl" ILIKE '%' || OLD."Id"::text || '%'
    ) THEN
        RAISE EXCEPTION 'media_in_use_banner' USING ERRCODE = '23503';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS "TR_SpellIcons_ProtectBannerMedia" ON "SpellIcons";
CREATE TRIGGER "TR_SpellIcons_ProtectBannerMedia" BEFORE UPDATE OF "IsDeleted" ON "SpellIcons" FOR EACH ROW EXECUTE FUNCTION ph_protect_banner_media();
""";

    public static async Task EnsureSchemaAsync(AppDbContext db, CancellationToken ct) => await db.Database.ExecuteSqlRawAsync(Schema, ct);

    public static string NormalizePath(string? path)
    {
        var value = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (!value.StartsWith('/')) value = "/" + value;
        if (value.Length > 1) value = value.TrimEnd('/');
        return value;
    }

    public static string? Validate(BannerRequest request, bool creating)
    {
        if (string.IsNullOrWhiteSpace(request.InternalName) || request.InternalName.Trim().Length > 100) return "Internal name is required and limited to 100 characters.";
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200) return "Title is required and limited to 200 characters.";
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 10000) return "Text is required and limited to 10000 characters.";
        if ((request.AltText?.Length ?? 0) > 300 || (request.ButtonText?.Length ?? 0) > 120 || (request.ImageUrl?.Length ?? 0) > 2048 || (request.ButtonUrl?.Length ?? 0) > 2048) return "One or more values exceed their maximum length.";
        if (!new[] { "information", "success", "warning" }.Contains(request.Kind?.Trim().ToLowerInvariant())) return "Kind must be information, success or warning.";
        if (!new[] { "above-navbar", "below-navbar", "above-content" }.Contains(request.Position?.Trim().ToLowerInvariant())) return "Position is invalid.";
        if (request.StartAtUtc.HasValue && request.EndAtUtc.HasValue && request.EndAtUtc.Value.ToUniversalTime() < request.StartAtUtc.Value.ToUniversalTime()) return "End date cannot be before start date.";
        if (!string.IsNullOrWhiteSpace(request.ButtonUrl) && !SafeUrl(request.ButtonUrl)) return "Button URL must be an internal path or an HTTP/HTTPS URL.";
        if (!string.IsNullOrWhiteSpace(request.ImageUrl) && !SafeUrl(request.ImageUrl)) return "Image URL must be an internal path or an HTTP/HTTPS URL.";
        if (request.Pages is { Count: > 200 }) return "A banner can target at most 200 pages.";
        if (request.Pages is not null && request.Pages.Any(p => string.IsNullOrWhiteSpace(p) || !NormalizePath(p).StartsWith('/'))) return "Page scopes must be public paths.";
        if (!creating && request.Version < 1) return "Version is required.";
        return null;
    }

    private static bool SafeUrl(string value)
    {
        value = value.Trim();
        if (value.StartsWith('/') && !value.StartsWith("//")) return true;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme is "http" or "https";
    }

    public static async Task<List<BannerDto>> ReadAllAsync(AppDbContext db, CancellationToken ct)
    {
        var connection = await ConnectionAsync(db, ct);
        await using var command = new NpgsqlCommand("SELECT * FROM \"SiteBanners\" ORDER BY \"Position\", \"SortOrder\", \"InternalName\"", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<BannerDto>();
        while (await reader.ReadAsync(ct)) result.Add(Read(reader));
        return result;
    }

    public static async Task<BannerDto> CreateAsync(AppDbContext db, BannerRequest request, string actor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new BannerDto(Guid.NewGuid(), request.InternalName.Trim(), request.Title.Trim(), request.Text.Trim(), Null(request.ImageUrl), request.AltText?.Trim() ?? "", Null(request.ButtonText), Null(request.ButtonUrl), request.Kind.Trim().ToLowerInvariant(), request.Position.Trim().ToLowerInvariant(), NormalizePages(request.Pages), request.StartAtUtc?.ToUniversalTime(), request.EndAtUtc?.ToUniversalTime(), request.SortOrder, request.IsDismissible, request.IsActive, false, false, 1, now, now);
        var connection = await ConnectionAsync(db, ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        try
        {
            await InsertAsync(connection, tx, item, ct);
            await RevisionAsync(connection, tx, item, "created", actor, ct);
            await tx.CommitAsync(ct);
            return item;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync(ct);
            throw new InvalidOperationException("A banner with that internal name already exists.", ex);
        }
    }

    public static async Task<BannerStoreResult> UpdateAsync(AppDbContext db, Guid id, BannerRequest request, string actor, CancellationToken ct)
    {
        var current = await FindAsync(db, id, ct);
        if (current is null || current.IsDeleted) return new(404, "banner.notFound", "Banner not found.");
        if (current.Version != request.Version) return new(409, "banner.stale", "The banner was changed by another user. Reload before saving.");
        var next = current with
        {
            InternalName = request.InternalName.Trim(), Title = request.Title.Trim(), Text = request.Text.Trim(), ImageUrl = Null(request.ImageUrl), AltText = request.AltText?.Trim() ?? "",
            ButtonText = Null(request.ButtonText), ButtonUrl = Null(request.ButtonUrl), Kind = request.Kind.Trim().ToLowerInvariant(), Position = request.Position.Trim().ToLowerInvariant(),
            Pages = NormalizePages(request.Pages), StartAtUtc = request.StartAtUtc?.ToUniversalTime(), EndAtUtc = request.EndAtUtc?.ToUniversalTime(), SortOrder = request.SortOrder,
            IsDismissible = request.IsDismissible, IsActive = request.IsActive, Version = current.Version + 1, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        return await ReplaceAsync(db, current, next, "updated", actor, ct);
    }

    public static async Task<BannerStoreResult> ChangeAsync(AppDbContext db, Guid id, BannerActionRequest request, string actor, CancellationToken ct)
    {
        var current = await FindAsync(db, id, ct);
        if (current is null) return new(404, "banner.notFound", "Banner not found.");
        if (current.Version != request.Version) return new(409, "banner.stale", "The banner was changed by another user. Reload before continuing.");
        var action = request.Action.Trim().ToLowerInvariant();
        BannerDto next;
        switch (action)
        {
            case "archive": next = current with { IsArchived = true, Version = current.Version + 1, UpdatedAtUtc = DateTimeOffset.UtcNow }; break;
            case "unarchive": next = current with { IsArchived = false, IsDeleted = false, Version = current.Version + 1, UpdatedAtUtc = DateTimeOffset.UtcNow }; break;
            case "delete": next = current with { IsDeleted = true, IsActive = false, Version = current.Version + 1, UpdatedAtUtc = DateTimeOffset.UtcNow }; break;
            case "restore":
                if (request.RevisionId is null) return new(400, "banner.revisionRequired", "Revision is required.");
                var snapshot = await RevisionSnapshotAsync(db, id, request.RevisionId.Value, ct);
                if (snapshot is null) return new(404, "banner.revisionNotFound", "Revision not found.");
                var restoredValidation = Validate(ToRequest(snapshot, current.Version), creating: false);
                if (restoredValidation is not null) return new(400, "banner.restoreInvalid", restoredValidation);
                next = snapshot with { Version = current.Version + 1, IsDeleted = false, UpdatedAtUtc = DateTimeOffset.UtcNow };
                break;
            default: return new(400, "banner.actionInvalid", "Unsupported banner action.");
        }
        return await ReplaceAsync(db, current, next, action == "restore" ? "restored" : action + "d", actor, ct);
    }

    public static async Task<List<BannerRevisionDto>> HistoryAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var connection = await ConnectionAsync(db, ct);
        await using var command = new NpgsqlCommand("SELECT \"Id\",\"BannerId\",\"Version\",\"Action\",\"Actor\",\"CreatedAtUtc\",\"Snapshot\"::text FROM \"BannerRevisions\" WHERE \"BannerId\"=@id ORDER BY \"Version\" DESC", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<BannerRevisionDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5), JsonSerializer.Deserialize<BannerDto>(reader.GetString(6))!));
        return result;
    }

    private static async Task<BannerStoreResult> ReplaceAsync(AppDbContext db, BannerDto current, BannerDto next, string action, string actor, CancellationToken ct)
    {
        var connection = await ConnectionAsync(db, ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
UPDATE "SiteBanners" SET "InternalName"=@name,"Title"=@title,"Text"=@text,"ImageUrl"=@image,"AltText"=@alt,"ButtonText"=@buttonText,"ButtonUrl"=@buttonUrl,"Kind"=@kind,"Position"=@position,"PagesJson"=CAST(@pages AS jsonb),"StartAtUtc"=@start,"EndAtUtc"=@end,"SortOrder"=@sort,"IsDismissible"=@dismiss,"IsActive"=@active,"IsArchived"=@archived,"IsDeleted"=@deleted,"Version"=@nextVersion,"UpdatedAtUtc"=@updated WHERE "Id"=@id AND "Version"=@version
""", connection, tx);
        Bind(command, next);
        command.Parameters.AddWithValue("id", current.Id);
        command.Parameters.AddWithValue("version", current.Version);
        var changed = await command.ExecuteNonQueryAsync(ct);
        if (changed == 0) { await tx.RollbackAsync(ct); return new(409, "banner.stale", "The banner was changed by another user. Reload before continuing."); }
        await RevisionAsync(connection, tx, next, action, actor, ct);
        await tx.CommitAsync(ct);
        return new(200, "ok", "", next);
    }

    private static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction tx, BannerDto item, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
INSERT INTO "SiteBanners" ("Id","InternalName","Title","Text","ImageUrl","AltText","ButtonText","ButtonUrl","Kind","Position","PagesJson","StartAtUtc","EndAtUtc","SortOrder","IsDismissible","IsActive","IsArchived","IsDeleted","Version","CreatedAtUtc","UpdatedAtUtc") VALUES (@id,@name,@title,@text,@image,@alt,@buttonText,@buttonUrl,@kind,@position,CAST(@pages AS jsonb),@start,@end,@sort,@dismiss,@active,@archived,@deleted,@nextVersion,@created,@updated)
""", connection, tx);
        Bind(command, item);
        command.Parameters.AddWithValue("id", item.Id);
        command.Parameters.AddWithValue("created", item.CreatedAtUtc);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void Bind(NpgsqlCommand command, BannerDto item)
    {
        command.Parameters.AddWithValue("name", item.InternalName);
        command.Parameters.AddWithValue("title", item.Title);
        command.Parameters.AddWithValue("text", item.Text);
        command.Parameters.Add(new NpgsqlParameter("image", NpgsqlDbType.Varchar) { Value = (object?)item.ImageUrl ?? DBNull.Value });
        command.Parameters.AddWithValue("alt", item.AltText);
        command.Parameters.Add(new NpgsqlParameter("buttonText", NpgsqlDbType.Varchar) { Value = (object?)item.ButtonText ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("buttonUrl", NpgsqlDbType.Varchar) { Value = (object?)item.ButtonUrl ?? DBNull.Value });
        command.Parameters.AddWithValue("kind", item.Kind);
        command.Parameters.AddWithValue("position", item.Position);
        command.Parameters.AddWithValue("pages", JsonSerializer.Serialize(item.Pages));
        command.Parameters.Add(new NpgsqlParameter("start", NpgsqlDbType.TimestampTz) { Value = (object?)item.StartAtUtc ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("end", NpgsqlDbType.TimestampTz) { Value = (object?)item.EndAtUtc ?? DBNull.Value });
        command.Parameters.AddWithValue("sort", item.SortOrder);
        command.Parameters.AddWithValue("dismiss", item.IsDismissible);
        command.Parameters.AddWithValue("active", item.IsActive);
        command.Parameters.AddWithValue("archived", item.IsArchived);
        command.Parameters.AddWithValue("deleted", item.IsDeleted);
        command.Parameters.AddWithValue("nextVersion", item.Version);
        command.Parameters.AddWithValue("updated", item.UpdatedAtUtc);
    }

    private static async Task RevisionAsync(NpgsqlConnection connection, NpgsqlTransaction tx, BannerDto item, string action, string actor, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("INSERT INTO \"BannerRevisions\" (\"Id\",\"BannerId\",\"Version\",\"Action\",\"Actor\",\"CreatedAtUtc\",\"Snapshot\") VALUES (@id,@banner,@version,@action,@actor,@created,CAST(@snapshot AS jsonb))", connection, tx);
        command.Parameters.AddWithValue("id", Guid.NewGuid()); command.Parameters.AddWithValue("banner", item.Id); command.Parameters.AddWithValue("version", item.Version); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("created", DateTimeOffset.UtcNow); command.Parameters.AddWithValue("snapshot", JsonSerializer.Serialize(item));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<BannerDto?> RevisionSnapshotAsync(AppDbContext db, Guid id, Guid revisionId, CancellationToken ct)
    {
        var connection = await ConnectionAsync(db, ct);
        await using var command = new NpgsqlCommand("SELECT \"Snapshot\"::text FROM \"BannerRevisions\" WHERE \"BannerId\"=@banner AND \"Id\"=@id", connection);
        command.Parameters.AddWithValue("banner", id); command.Parameters.AddWithValue("id", revisionId);
        var value = await command.ExecuteScalarAsync(ct) as string;
        return value is null ? null : JsonSerializer.Deserialize<BannerDto>(value);
    }

    private static async Task<BannerDto?> FindAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var connection = await ConnectionAsync(db, ct);
        await using var command = new NpgsqlCommand("SELECT * FROM \"SiteBanners\" WHERE \"Id\"=@id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    private static BannerDto Read(NpgsqlDataReader r)
    {
        var pages = JsonSerializer.Deserialize<List<string>>(r.GetFieldValue<string>(r.GetOrdinal("PagesJson"))) ?? [];
        DateTimeOffset? Time(string name) => r.IsDBNull(r.GetOrdinal(name)) ? null : r.GetFieldValue<DateTimeOffset>(r.GetOrdinal(name));
        string? Text(string name) => r.IsDBNull(r.GetOrdinal(name)) ? null : r.GetString(r.GetOrdinal(name));
        return new(r.GetGuid(r.GetOrdinal("Id")), r.GetString(r.GetOrdinal("InternalName")), r.GetString(r.GetOrdinal("Title")), r.GetString(r.GetOrdinal("Text")), Text("ImageUrl"), r.GetString(r.GetOrdinal("AltText")), Text("ButtonText"), Text("ButtonUrl"), r.GetString(r.GetOrdinal("Kind")), r.GetString(r.GetOrdinal("Position")), pages, Time("StartAtUtc"), Time("EndAtUtc"), r.GetInt32(r.GetOrdinal("SortOrder")), r.GetBoolean(r.GetOrdinal("IsDismissible")), r.GetBoolean(r.GetOrdinal("IsActive")), r.GetBoolean(r.GetOrdinal("IsArchived")), r.GetBoolean(r.GetOrdinal("IsDeleted")), r.GetInt32(r.GetOrdinal("Version")), r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("CreatedAtUtc")), r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("UpdatedAtUtc")));
    }

    private static async Task<NpgsqlConnection> ConnectionAsync(AppDbContext db, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        return connection;
    }

    private static IReadOnlyList<string> NormalizePages(IReadOnlyList<string>? pages) => pages?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToArray() ?? [];
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static BannerRequest ToRequest(BannerDto x, int version) => new(x.InternalName, x.Title, x.Text, x.ImageUrl, x.AltText, x.ButtonText, x.ButtonUrl, x.Kind, x.Position, x.Pages, x.StartAtUtc, x.EndAtUtc, x.SortOrder, x.IsDismissible, x.IsActive, version);
}
