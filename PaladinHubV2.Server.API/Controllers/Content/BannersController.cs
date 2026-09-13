using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.API.Controllers.Content;

public sealed record BannerRequest(string InternalName,string Title,string Text,string? ImageUrl,string? AltText,string? ButtonText,string? ButtonUrl,string Kind,string Position,IReadOnlyList<string>? Pages,DateTimeOffset? StartAtUtc,DateTimeOffset? EndAtUtc,int SortOrder,bool IsDismissible,bool IsActive,int Version);
public sealed record BannerActionRequest(int Version,string Action,Guid? RevisionId);
public sealed record BannerDto(Guid Id,string InternalName,string Title,string Text,string? ImageUrl,string AltText,string? ButtonText,string? ButtonUrl,string Kind,string Position,IReadOnlyList<string> Pages,DateTimeOffset? StartAtUtc,DateTimeOffset? EndAtUtc,int SortOrder,bool IsDismissible,bool IsActive,bool IsArchived,bool IsDeleted,int Version,DateTimeOffset CreatedAtUtc,DateTimeOffset UpdatedAtUtc);
public sealed record BannerRevisionDto(Guid Id,Guid BannerId,int Version,string Action,string Actor,DateTimeOffset CreatedAtUtc,BannerDto Snapshot);

[ApiController,Authorize(Roles="Admin"),Route("Admin/api/banners")]
public sealed class BannersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]string? search,[FromQuery]string status="active",CancellationToken ct=default)
    {
        await BannerStore.EnsureSchemaAsync(db,ct);
        IEnumerable<BannerDto> items=await BannerStore.ReadAllAsync(db,ct);
        if(!string.IsNullOrWhiteSpace(search)){var n=search.Trim();items=items.Where(x=>x.InternalName.Contains(n,StringComparison.OrdinalIgnoreCase)||x.Title.Contains(n,StringComparison.OrdinalIgnoreCase)||x.Text.Contains(n,StringComparison.OrdinalIgnoreCase));}
        items=status.ToLowerInvariant() switch {"all"=>items,"archived"=>items.Where(x=>x.IsArchived&&!x.IsDeleted),"deleted"=>items.Where(x=>x.IsDeleted),"inactive"=>items.Where(x=>!x.IsDeleted&&!x.IsArchived&&!x.IsActive),_=>items.Where(x=>!x.IsDeleted&&!x.IsArchived&&x.IsActive)};
        return Ok(items.OrderBy(x=>x.Position).ThenBy(x=>x.SortOrder).ThenBy(x=>x.InternalName));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id,CancellationToken ct){await BannerStore.EnsureSchemaAsync(db,ct);return Ok(await BannerStore.HistoryAsync(db,id,ct));}

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BannerRequest request,CancellationToken ct)
    {
        var invalid=BannerStore.Validate(request,true);if(invalid is not null)return BadRequest(new{code="banner.validation",message=invalid});
        await BannerStore.EnsureSchemaAsync(db,ct);
        var result=await BannerStore.CreateAsync(db,request,User.Identity?.Name??"admin",ct);return Result(result,true);
    }

    [HttpPut("{id:guid}"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id,BannerRequest request,CancellationToken ct)
    {
        var invalid=BannerStore.Validate(request,false);if(invalid is not null)return BadRequest(new{code="banner.validation",message=invalid});
        await BannerStore.EnsureSchemaAsync(db,ct);return Result(await BannerStore.UpdateAsync(db,id,request,User.Identity?.Name??"admin",ct));
    }

    [HttpPost("{id:guid}/actions"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(Guid id,BannerActionRequest request,CancellationToken ct)
    {await BannerStore.EnsureSchemaAsync(db,ct);return Result(await BannerStore.ChangeAsync(db,id,request,User.Identity?.Name??"admin",ct));}

    private IActionResult Result(BannerStoreResult r,bool created=false)=>r.Status switch{200 when r.Banner is not null&&created=>Created($"/Admin/api/banners/{r.Banner.Id}",r.Banner),200 when r.Banner is not null=>Ok(r.Banner),404=>NotFound(new{code=r.Code,message=r.Message}),409=>Conflict(new{code=r.Code,message=r.Message}),_=>BadRequest(new{code=r.Code,message=r.Message})};
}

[ApiController,Route("api/banners")]
public sealed class PublicBannersController(AppDbContext db):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Visible([FromQuery]string path="/",CancellationToken ct=default)
    {
        await BannerStore.EnsureSchemaAsync(db,ct);path=BannerStore.NormalizePath(path);var now=DateTimeOffset.UtcNow;
        var scoped=(await BannerStore.ReadAllAsync(db,ct)).Where(x=>!x.IsDeleted&&!x.IsArchived&&x.IsActive).Where(x=>x.Pages.Count==0||x.Pages.Any(p=>string.Equals(BannerStore.NormalizePath(p),path,StringComparison.OrdinalIgnoreCase))).ToList();
        var visible=scoped.Where(x=>(x.StartAtUtc is null||x.StartAtUtc<=now)&&(x.EndAtUtc is null||x.EndAtUtc>=now)).OrderBy(x=>x.Position).ThenBy(x=>x.SortOrder).ThenBy(x=>x.Id).Select(x=>new{x.Id,x.Title,x.Text,x.ImageUrl,x.AltText,x.ButtonText,x.ButtonUrl,x.Kind,x.Position,x.StartAtUtc,x.EndAtUtc,x.SortOrder,x.IsDismissible,x.Version,translationKeys=new{title=$"banner.{x.Id:N}.title",text=$"banner.{x.Id:N}.text",alt=$"banner.{x.Id:N}.alt",button=$"banner.{x.Id:N}.button"}}).ToArray();
        var next=scoped.SelectMany(x=>new[]{x.StartAtUtc,x.EndAtUtc}).Where(x=>x.HasValue&&x.Value>now).Select(x=>x!.Value).OrderBy(x=>x).Cast<DateTimeOffset?>().FirstOrDefault();
        return Ok(new{items=visible,nextChangeAtUtc=next});
    }
}

internal sealed record BannerStoreResult(int Status,string Code,string Message,BannerDto? Banner=null);
internal static class BannerStore
{
    private const string Schema="""
CREATE TABLE IF NOT EXISTS "SiteBanners"("Id" uuid PRIMARY KEY,"InternalName" varchar(100) NOT NULL,"Title" varchar(200) NOT NULL,"Text" text NOT NULL,"ImageUrl" varchar(2048) NULL,"AltText" varchar(300) NOT NULL DEFAULT '',"ButtonText" varchar(120) NULL,"ButtonUrl" varchar(2048) NULL,"Kind" varchar(16) NOT NULL,"Position" varchar(32) NOT NULL,"PagesJson" jsonb NOT NULL DEFAULT '[]'::jsonb,"StartAtUtc" timestamptz NULL,"EndAtUtc" timestamptz NULL,"SortOrder" integer NOT NULL DEFAULT 0,"IsDismissible" boolean NOT NULL DEFAULT true,"IsActive" boolean NOT NULL DEFAULT true,"IsArchived" boolean NOT NULL DEFAULT false,"IsDeleted" boolean NOT NULL DEFAULT false,"Version" integer NOT NULL DEFAULT 1,"CreatedAtUtc" timestamptz NOT NULL,"UpdatedAtUtc" timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SiteBanners_InternalName" ON "SiteBanners"(lower("InternalName")) WHERE NOT "IsDeleted";
CREATE INDEX IF NOT EXISTS "IX_SiteBanners_Visibility" ON "SiteBanners"("IsDeleted","IsArchived","IsActive","StartAtUtc","EndAtUtc","Position","SortOrder");
CREATE TABLE IF NOT EXISTS "BannerRevisions"("Id" uuid PRIMARY KEY,"BannerId" uuid NOT NULL REFERENCES "SiteBanners"("Id") ON DELETE RESTRICT,"Version" integer NOT NULL,"Action" varchar(32) NOT NULL,"Actor" varchar(256) NOT NULL,"CreatedAtUtc" timestamptz NOT NULL,"Snapshot" jsonb NOT NULL,CONSTRAINT "UQ_BannerRevisions_Banner_Version" UNIQUE("BannerId","Version"));
DO $$ BEGIN
 IF to_regclass('"SpellIcons"') IS NOT NULL THEN
  EXECUTE 'CREATE OR REPLACE FUNCTION ph_protect_banner_media() RETURNS trigger AS $f$ BEGIN IF NEW."IsDeleted" AND NOT OLD."IsDeleted" AND EXISTS (SELECT 1 FROM "SiteBanners" b WHERE NOT b."IsDeleted" AND b."ImageUrl" IS NOT NULL AND b."ImageUrl" ILIKE ''%'' || OLD."Id"::text || ''%'') THEN RAISE EXCEPTION ''media_in_use_banner'' USING ERRCODE = ''23503''; END IF; RETURN NEW; END; $f$ LANGUAGE plpgsql';
  DROP TRIGGER IF EXISTS "TR_SpellIcons_ProtectBannerMedia" ON "SpellIcons";
  CREATE TRIGGER "TR_SpellIcons_ProtectBannerMedia" BEFORE UPDATE OF "IsDeleted" ON "SpellIcons" FOR EACH ROW EXECUTE FUNCTION ph_protect_banner_media();
 END IF;
END $$;
""";
    public static async Task EnsureSchemaAsync(AppDbContext db,CancellationToken ct)=>await db.Database.ExecuteSqlRawAsync(Schema,ct);
    public static string NormalizePath(string? path){var v=string.IsNullOrWhiteSpace(path)?"/":path.Trim();if(!v.StartsWith('/'))v="/"+v;if(v.Length>1)v=v.TrimEnd('/');return v;}
    private static bool PublicPath(string p)=>p.StartsWith('/')&&!p.StartsWith("//")&&!p.Contains('\n')&&!p.Contains('\r');
    public static string? Validate(BannerRequest r,bool creating)
    {
        if(string.IsNullOrWhiteSpace(r.InternalName)||r.InternalName.Trim().Length>100)return "Internal name is required and limited to 100 characters.";
        if(string.IsNullOrWhiteSpace(r.Title)||r.Title.Trim().Length>200)return "Title is required and limited to 200 characters.";
        if(string.IsNullOrWhiteSpace(r.Text)||r.Text.Length>10000)return "Text is required and limited to 10000 characters.";
        if((r.AltText?.Length??0)>300||(r.ButtonText?.Length??0)>120||(r.ImageUrl?.Length??0)>2048||(r.ButtonUrl?.Length??0)>2048)return "One or more values exceed their maximum length.";
        if(!new[]{"information","success","warning"}.Contains(r.Kind?.Trim().ToLowerInvariant()))return "Kind must be information, success or warning.";
        if(!new[]{"above-navbar","below-navbar","above-content"}.Contains(r.Position?.Trim().ToLowerInvariant()))return "Position is invalid.";
        if(r.StartAtUtc.HasValue&&r.EndAtUtc.HasValue&&r.EndAtUtc.Value.ToUniversalTime()<r.StartAtUtc.Value.ToUniversalTime())return "End date cannot be before start date.";
        if(!string.IsNullOrWhiteSpace(r.ButtonUrl)&&!SafeUrl(r.ButtonUrl))return "Button URL must be an internal path or an HTTP/HTTPS URL.";
        if(!string.IsNullOrWhiteSpace(r.ImageUrl)&&!SafeUrl(r.ImageUrl))return "Image URL must be an internal path or an HTTP/HTTPS URL.";
        if(r.Pages is{Count:>200})return "A banner can target at most 200 pages.";
        if(r.Pages is not null&&r.Pages.Any(p=>string.IsNullOrWhiteSpace(p)||!PublicPath(p.Trim())))return "Page scopes must be public paths beginning with a single slash.";
        if(!creating&&r.Version<1)return "Version is required.";return null;
    }
    private static bool SafeUrl(string value){value=value.Trim();if(PublicPath(value))return true;if(!Uri.TryCreate(value,UriKind.Absolute,out var uri))return false;return uri.Scheme is "http" or "https";}
    public static async Task<List<BannerDto>> ReadAllAsync(AppDbContext db,CancellationToken ct){var c=await ConnectionAsync(db,ct);await using var cmd=new NpgsqlCommand("SELECT * FROM \"SiteBanners\" ORDER BY \"Position\",\"SortOrder\",\"InternalName\"",c);await using var reader=await cmd.ExecuteReaderAsync(ct);var list=new List<BannerDto>();while(await reader.ReadAsync(ct))list.Add(Read(reader));return list;}
    public static async Task<BannerStoreResult> CreateAsync(AppDbContext db,BannerRequest r,string actor,CancellationToken ct)
    {
        var now=DateTimeOffset.UtcNow;var x=new BannerDto(Guid.NewGuid(),r.InternalName.Trim(),r.Title.Trim(),r.Text.Trim(),Null(r.ImageUrl),r.AltText?.Trim()??"",Null(r.ButtonText),Null(r.ButtonUrl),r.Kind.Trim().ToLowerInvariant(),r.Position.Trim().ToLowerInvariant(),NormalizePages(r.Pages),r.StartAtUtc?.ToUniversalTime(),r.EndAtUtc?.ToUniversalTime(),r.SortOrder,r.IsDismissible,r.IsActive,false,false,1,now,now);var c=await ConnectionAsync(db,ct);await using var tx=await c.BeginTransactionAsync(ct);
        try{await InsertAsync(c,tx,x,ct);await RevisionAsync(c,tx,x,"created",actor,ct);await tx.CommitAsync(ct);return new(200,"ok","",x);}catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){await tx.RollbackAsync(ct);return new(409,"banner.duplicate","A banner with that internal name already exists.");}
    }
    public static async Task<BannerStoreResult> UpdateAsync(AppDbContext db,Guid id,BannerRequest r,string actor,CancellationToken ct){var current=await FindAsync(db,id,ct);if(current is null||current.IsDeleted)return new(404,"banner.notFound","Banner not found.");if(current.Version!=r.Version)return new(409,"banner.stale","The banner was changed by another user. Reload before saving.");var next=current with{InternalName=r.InternalName.Trim(),Title=r.Title.Trim(),Text=r.Text.Trim(),ImageUrl=Null(r.ImageUrl),AltText=r.AltText?.Trim()??"",ButtonText=Null(r.ButtonText),ButtonUrl=Null(r.ButtonUrl),Kind=r.Kind.Trim().ToLowerInvariant(),Position=r.Position.Trim().ToLowerInvariant(),Pages=NormalizePages(r.Pages),StartAtUtc=r.StartAtUtc?.ToUniversalTime(),EndAtUtc=r.EndAtUtc?.ToUniversalTime(),SortOrder=r.SortOrder,IsDismissible=r.IsDismissible,IsActive=r.IsActive,Version=current.Version+1,UpdatedAtUtc=DateTimeOffset.UtcNow};return await ReplaceAsync(db,current,next,"updated",actor,ct);}
    public static async Task<BannerStoreResult> ChangeAsync(AppDbContext db,Guid id,BannerActionRequest r,string actor,CancellationToken ct)
    {
        var current=await FindAsync(db,id,ct);if(current is null)return new(404,"banner.notFound","Banner not found.");if(current.Version!=r.Version)return new(409,"banner.stale","The banner was changed by another user. Reload before continuing.");var action=r.Action.Trim().ToLowerInvariant();BannerDto next;
        switch(action){case"archive":next=current with{IsArchived=true,Version=current.Version+1,UpdatedAtUtc=DateTimeOffset.UtcNow};break;case"unarchive":next=current with{IsArchived=false,IsDeleted=false,Version=current.Version+1,UpdatedAtUtc=DateTimeOffset.UtcNow};break;case"delete":next=current with{IsDeleted=true,IsActive=false,Version=current.Version+1,UpdatedAtUtc=DateTimeOffset.UtcNow};break;case"restore":if(r.RevisionId is null)return new(400,"banner.revisionRequired","Revision is required.");var snapshot=await RevisionSnapshotAsync(db,id,r.RevisionId.Value,ct);if(snapshot is null)return new(404,"banner.revisionNotFound","Revision not found.");var invalid=Validate(ToRequest(snapshot,current.Version),false);if(invalid is not null)return new(400,"banner.restoreInvalid",invalid);next=snapshot with{Version=current.Version+1,IsDeleted=false,UpdatedAtUtc=DateTimeOffset.UtcNow};break;default:return new(400,"banner.actionInvalid","Unsupported banner action.");}
        return await ReplaceAsync(db,current,next,action=="restore"?"restored":action+"d",actor,ct);
    }
    public static async Task<List<BannerRevisionDto>> HistoryAsync(AppDbContext db,Guid id,CancellationToken ct){var c=await ConnectionAsync(db,ct);await using var cmd=new NpgsqlCommand("SELECT \"Id\",\"BannerId\",\"Version\",\"Action\",\"Actor\",\"CreatedAtUtc\",\"Snapshot\"::text FROM \"BannerRevisions\" WHERE \"BannerId\"=@id ORDER BY \"Version\" DESC",c);cmd.Parameters.AddWithValue("id",id);await using var reader=await cmd.ExecuteReaderAsync(ct);var list=new List<BannerRevisionDto>();while(await reader.ReadAsync(ct))list.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetInt32(2),reader.GetString(3),reader.GetString(4),reader.GetFieldValue<DateTimeOffset>(5),JsonSerializer.Deserialize<BannerDto>(reader.GetString(6))!));return list;}
    private static async Task<BannerStoreResult> ReplaceAsync(AppDbContext db,BannerDto current,BannerDto next,string action,string actor,CancellationToken ct){var c=await ConnectionAsync(db,ct);await using var tx=await c.BeginTransactionAsync(ct);try{await using var cmd=new NpgsqlCommand("UPDATE \"SiteBanners\" SET \"InternalName\"=@name,\"Title\"=@title,\"Text\"=@text,\"ImageUrl\"=@image,\"AltText\"=@alt,\"ButtonText\"=@buttonText,\"ButtonUrl\"=@buttonUrl,\"Kind\"=@kind,\"Position\"=@position,\"PagesJson\"=CAST(@pages AS jsonb),\"StartAtUtc\"=@start,\"EndAtUtc\"=@end,\"SortOrder\"=@sort,\"IsDismissible\"=@dismiss,\"IsActive\"=@active,\"IsArchived\"=@archived,\"IsDeleted\"=@deleted,\"Version\"=@nextVersion,\"UpdatedAtUtc\"=@updated WHERE \"Id\"=@id AND \"Version\"=@version",c,tx);Bind(cmd,next);cmd.Parameters.AddWithValue("id",current.Id);cmd.Parameters.AddWithValue("version",current.Version);if(await cmd.ExecuteNonQueryAsync(ct)==0){await tx.RollbackAsync(ct);return new(409,"banner.stale","The banner was changed by another user. Reload before continuing.");}await RevisionAsync(c,tx,next,action,actor,ct);await tx.CommitAsync(ct);return new(200,"ok","",next);}catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){await tx.RollbackAsync(ct);return new(409,"banner.duplicate","A banner with that internal name already exists.");}}
    private static async Task InsertAsync(NpgsqlConnection c,NpgsqlTransaction tx,BannerDto x,CancellationToken ct){await using var cmd=new NpgsqlCommand("INSERT INTO \"SiteBanners\"(\"Id\",\"InternalName\",\"Title\",\"Text\",\"ImageUrl\",\"AltText\",\"ButtonText\",\"ButtonUrl\",\"Kind\",\"Position\",\"PagesJson\",\"StartAtUtc\",\"EndAtUtc\",\"SortOrder\",\"IsDismissible\",\"IsActive\",\"IsArchived\",\"IsDeleted\",\"Version\",\"CreatedAtUtc\",\"UpdatedAtUtc\") VALUES(@id,@name,@title,@text,@image,@alt,@buttonText,@buttonUrl,@kind,@position,CAST(@pages AS jsonb),@start,@end,@sort,@dismiss,@active,@archived,@deleted,@nextVersion,@created,@updated)",c,tx);Bind(cmd,x);cmd.Parameters.AddWithValue("id",x.Id);cmd.Parameters.AddWithValue("created",x.CreatedAtUtc);await cmd.ExecuteNonQueryAsync(ct);}
    private static void Bind(NpgsqlCommand cmd,BannerDto x){cmd.Parameters.AddWithValue("name",x.InternalName);cmd.Parameters.AddWithValue("title",x.Title);cmd.Parameters.AddWithValue("text",x.Text);cmd.Parameters.Add(new NpgsqlParameter("image",NpgsqlDbType.Varchar){Value=(object?)x.ImageUrl??DBNull.Value});cmd.Parameters.AddWithValue("alt",x.AltText);cmd.Parameters.Add(new NpgsqlParameter("buttonText",NpgsqlDbType.Varchar){Value=(object?)x.ButtonText??DBNull.Value});cmd.Parameters.Add(new NpgsqlParameter("buttonUrl",NpgsqlDbType.Varchar){Value=(object?)x.ButtonUrl??DBNull.Value});cmd.Parameters.AddWithValue("kind",x.Kind);cmd.Parameters.AddWithValue("position",x.Position);cmd.Parameters.AddWithValue("pages",JsonSerializer.Serialize(x.Pages));cmd.Parameters.Add(new NpgsqlParameter("start",NpgsqlDbType.TimestampTz){Value=(object?)x.StartAtUtc??DBNull.Value});cmd.Parameters.Add(new NpgsqlParameter("end",NpgsqlDbType.TimestampTz){Value=(object?)x.EndAtUtc??DBNull.Value});cmd.Parameters.AddWithValue("sort",x.SortOrder);cmd.Parameters.AddWithValue("dismiss",x.IsDismissible);cmd.Parameters.AddWithValue("active",x.IsActive);cmd.Parameters.AddWithValue("archived",x.IsArchived);cmd.Parameters.AddWithValue("deleted",x.IsDeleted);cmd.Parameters.AddWithValue("nextVersion",x.Version);cmd.Parameters.AddWithValue("updated",x.UpdatedAtUtc);}
    private static async Task RevisionAsync(NpgsqlConnection c,NpgsqlTransaction tx,BannerDto x,string action,string actor,CancellationToken ct){await using var cmd=new NpgsqlCommand("INSERT INTO \"BannerRevisions\"(\"Id\",\"BannerId\",\"Version\",\"Action\",\"Actor\",\"CreatedAtUtc\",\"Snapshot\") VALUES(@id,@banner,@version,@action,@actor,@created,CAST(@snapshot AS jsonb))",c,tx);cmd.Parameters.AddWithValue("id",Guid.NewGuid());cmd.Parameters.AddWithValue("banner",x.Id);cmd.Parameters.AddWithValue("version",x.Version);cmd.Parameters.AddWithValue("action",action);cmd.Parameters.AddWithValue("actor",actor);cmd.Parameters.AddWithValue("created",DateTimeOffset.UtcNow);cmd.Parameters.AddWithValue("snapshot",JsonSerializer.Serialize(x));await cmd.ExecuteNonQueryAsync(ct);}
    private static async Task<BannerDto?> RevisionSnapshotAsync(AppDbContext db,Guid banner,Guid id,CancellationToken ct){var c=await ConnectionAsync(db,ct);await using var cmd=new NpgsqlCommand("SELECT \"Snapshot\"::text FROM \"BannerRevisions\" WHERE \"BannerId\"=@banner AND \"Id\"=@id",c);cmd.Parameters.AddWithValue("banner",banner);cmd.Parameters.AddWithValue("id",id);var v=await cmd.ExecuteScalarAsync(ct) as string;return v is null?null:JsonSerializer.Deserialize<BannerDto>(v);}
    private static async Task<BannerDto?> FindAsync(AppDbContext db,Guid id,CancellationToken ct){var c=await ConnectionAsync(db,ct);await using var cmd=new NpgsqlCommand("SELECT * FROM \"SiteBanners\" WHERE \"Id\"=@id",c);cmd.Parameters.AddWithValue("id",id);await using var reader=await cmd.ExecuteReaderAsync(ct);return await reader.ReadAsync(ct)?Read(reader):null;}
    private static BannerDto Read(NpgsqlDataReader r){DateTimeOffset? Time(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetFieldValue<DateTimeOffset>(r.GetOrdinal(n));string? Text(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:r.GetString(r.GetOrdinal(n));var pages=JsonSerializer.Deserialize<List<string>>(r.GetString(r.GetOrdinal("PagesJson")))??[];return new(r.GetGuid(r.GetOrdinal("Id")),r.GetString(r.GetOrdinal("InternalName")),r.GetString(r.GetOrdinal("Title")),r.GetString(r.GetOrdinal("Text")),Text("ImageUrl"),r.GetString(r.GetOrdinal("AltText")),Text("ButtonText"),Text("ButtonUrl"),r.GetString(r.GetOrdinal("Kind")),r.GetString(r.GetOrdinal("Position")),pages,Time("StartAtUtc"),Time("EndAtUtc"),r.GetInt32(r.GetOrdinal("SortOrder")),r.GetBoolean(r.GetOrdinal("IsDismissible")),r.GetBoolean(r.GetOrdinal("IsActive")),r.GetBoolean(r.GetOrdinal("IsArchived")),r.GetBoolean(r.GetOrdinal("IsDeleted")),r.GetInt32(r.GetOrdinal("Version")),r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("CreatedAtUtc")),r.GetFieldValue<DateTimeOffset>(r.GetOrdinal("UpdatedAtUtc")));}
    private static async Task<NpgsqlConnection> ConnectionAsync(AppDbContext db,CancellationToken ct){var c=(NpgsqlConnection)db.Database.GetDbConnection();if(c.State!=ConnectionState.Open)await c.OpenAsync(ct);return c;}
    private static IReadOnlyList<string> NormalizePages(IReadOnlyList<string>? p)=>p?.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>NormalizePath(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToArray()??[];
    private static string? Null(string? v)=>string.IsNullOrWhiteSpace(v)?null:v.Trim();
    private static BannerRequest ToRequest(BannerDto x,int version)=>new(x.InternalName,x.Title,x.Text,x.ImageUrl,x.AltText,x.ButtonText,x.ButtonUrl,x.Kind,x.Position,x.Pages,x.StartAtUtc,x.EndAtUtc,x.SortOrder,x.IsDismissible,x.IsActive,version);
}
