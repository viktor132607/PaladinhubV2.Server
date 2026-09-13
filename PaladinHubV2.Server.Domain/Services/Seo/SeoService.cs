using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
namespace PaladinHubV2.Server.Domain.Services.Seo;
public sealed record SeoRequest(int? PageId,string Path,string Title,string Description,string CanonicalUrl,string SocialTitle,string SocialDescription,string ImageUrl,bool? Index,bool? Follow,int Version);
public sealed record SeoResult(int Status,string? Message=null,SeoEntry? Entry=null);
public sealed class SeoService(AppDbContext db)
{
 public static bool PublicPath(string path)=>path=="*"||(!string.IsNullOrWhiteSpace(path)&&path.Length<=2048&&path.StartsWith('/')&&!path.StartsWith("//")&&!path.Any(char.IsControl)&&!path.Contains('\\')&&!path.Contains('?')&&!path.Contains('#')&&!path.Split('/').Any(p=>p is "." or "..")&&!Regex.IsMatch(path,@"^/(admin|api|account|checkout|cart|login|register|error|verify-email)(/|$)|^/products/(create|edit)(/|$)|/view$",RegexOptions.IgnoreCase));
 public static string NormalizePath(string path) => path == "*" ? "*" : path.TrimEnd('/') is { Length: > 0 } trimmed ? trimmed : "/";
 public static string? Validate(SeoRequest r)
 {
  if(!PublicPath(r.Path))return "Choose a public route or * for defaults.";
  if(r.Title is null||r.Title.Length>200||r.Description is null||r.Description.Length>500||r.SocialTitle is null||r.SocialTitle.Length>200||r.SocialDescription is null||r.SocialDescription.Length>500)return "Titles allow 200 characters; descriptions allow 500.";
  foreach(var value in new[]{r.CanonicalUrl,r.ImageUrl})if(value is null||value.Length>2048||value.Any(char.IsControl)||value.Contains('\\')||(value!=""&&(!Uri.TryCreate(value,UriKind.Absolute,out var u)||u.Scheme is not ("http" or "https"))))return "Canonical and social image must be absolute HTTP/HTTPS URLs.";
  if(r.Path=="*"&&r.CanonicalUrl!="")return "Set canonical URLs per page, not globally.";
  return null;
 }
 public Task<List<SeoEntry>> List(CancellationToken ct)=>db.SeoEntries.AsNoTracking().OrderBy(x=>x.Path).ToListAsync(ct);
 public Task<List<SeoRevision>> History(Guid id,CancellationToken ct)=>db.SeoRevisions.AsNoTracking().Where(x=>x.EntryId==id).OrderByDescending(x=>x.Version).ToListAsync(ct);
 private async Task<string?> Relations(Guid id,SeoRequest r,CancellationToken ct)
 {
  if(r.PageId.HasValue&&!await db.ContentPages.IgnoreQueryFilters().AnyAsync(x=>x.Id==r.PageId&&!x.IsDeleted&&!x.IsArchived,ct))return "Choose an existing, unarchived page.";
  if(await db.SeoEntries.AnyAsync(x=>x.Id!=id&&!x.IsDeleted&&(r.PageId.HasValue?x.PageId==r.PageId:x.PageId==null&&x.Path.ToLower()==r.Path.ToLower()),ct))return "SEO settings already exist for this target. Restore or edit that record.";
  var match=Regex.Match(r.ImageUrl,@"/(?:spell-icons|icons)/([0-9a-fA-F-]{36})(?:$|[/?#])");
  if(match.Success&&Guid.TryParse(match.Groups[1].Value,out var mediaId)&&!await db.SpellIcons.AnyAsync(x=>x.Id==mediaId&&!x.IsDeleted&&!x.IsArchived,ct))return "Select an active social image.";
  return null;
 }
 public async Task<SeoResult> Save(Guid? id,SeoRequest r,string actor,CancellationToken ct)
 {
  var error=Validate(r);if(error is not null)return new(400,error);r=r with { Path=NormalizePath(r.Path) };await using var tx=await new GameDataAssignmentService(db).BeginAsync(ct);
  var entry=id.HasValue?await db.SeoEntries.SingleOrDefaultAsync(x=>x.Id==id,ct):new SeoEntry();if(entry is null)return new(404,"SEO entry not found.");
  if(id.HasValue&&entry.Version!=r.Version)return new(409,"Settings changed. Reload before saving.");if(entry.IsDeleted||entry.IsArchived)return new(409,"Restore or unarchive before editing.");
  error=await Relations(entry.Id,r,ct);if(error is not null)return new(409,error);Apply(entry,r);if(id.HasValue)entry.Version++;else db.SeoEntries.Add(entry);Record(entry,id.HasValue?"updated":"created",actor);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(200,Entry:entry);
 }
 public async Task<SeoResult> Change(Guid id,int version,string action,Guid? revisionId,string actor,CancellationToken ct)
 {
  await using var tx=await new GameDataAssignmentService(db).BeginAsync(ct);var entry=await db.SeoEntries.SingleOrDefaultAsync(x=>x.Id==id,ct);if(entry is null)return new(404,"SEO entry not found.");if(entry.Version!=version)return new(409,"Settings changed. Reload before continuing.");
  if(action=="restore") {var rev=await db.SeoRevisions.SingleOrDefaultAsync(x=>x.EntryId==id&&x.Id==revisionId,ct);if(rev is null)return new(404,"Revision not found.");var old=JsonSerializer.Deserialize<SeoEntry>(rev.Snapshot)!;if(old.IsDeleted)return new(400,"Choose a revision before deletion.");var r=new SeoRequest(old.PageId,NormalizePath(old.Path),old.Title,old.Description,old.CanonicalUrl,old.SocialTitle,old.SocialDescription,old.ImageUrl,old.Index,old.Follow,version);var error=Validate(r)??await Relations(id,r,ct);if(error is not null)return new(409,error);Apply(entry,r);entry.IsDeleted=false;entry.IsArchived=old.IsArchived;}
  else {if(entry.IsDeleted)return new(409,"Restore this entry first.");if(action=="archive")entry.IsArchived=true;else if(action=="unarchive"){
   var request=new SeoRequest(entry.PageId,NormalizePath(entry.Path),entry.Title,entry.Description,entry.CanonicalUrl,entry.SocialTitle,entry.SocialDescription,entry.ImageUrl,entry.Index,entry.Follow,entry.Version);
   var error=Validate(request)??await Relations(id,request,ct);
   if(error is not null)return new(409,error);
   entry.IsArchived=false;
  }else if(action=="delete"){entry.IsArchived=true;entry.IsDeleted=true;}else return new(400,"Unknown action.");}
  entry.Version++;Record(entry,action,actor);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(200,Entry:entry);
 }
 private static void Apply(SeoEntry e,SeoRequest r){e.PageId=r.PageId;e.Path=NormalizePath(r.Path);e.Title=r.Title;e.Description=r.Description;e.CanonicalUrl=r.CanonicalUrl;e.SocialTitle=r.SocialTitle;e.SocialDescription=r.SocialDescription;e.ImageUrl=r.ImageUrl;e.Index=r.Index;e.Follow=r.Follow;}
 private void Record(SeoEntry e,string action,string actor)=>db.SeoRevisions.Add(new(){EntryId=e.Id,Version=e.Version,Action=action,Actor=actor[..Math.Min(actor.Length,100)],Snapshot=JsonSerializer.Serialize(e)});
}
