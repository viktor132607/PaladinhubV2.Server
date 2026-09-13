using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.Navigation;
namespace PaladinHubV2.Server.Domain.Services.Footer;
public sealed record FooterRequest(string Name,string Kind,string Text,string Url,string Icon,Guid? ParentId,int SortOrder,bool OpenNewTab,int Version);
public sealed record FooterResult(int Status,string? Message=null,FooterEntry? Entry=null);
public sealed class FooterService(AppDbContext db)
{
 public Task<List<FooterEntry>> List(CancellationToken ct) => db.FooterEntries.AsNoTracking().OrderBy(x=>x.SortOrder).ThenBy(x=>x.Name).ToListAsync(ct);
 public Task<List<FooterRevision>> History(Guid id,CancellationToken ct) => db.FooterRevisions.AsNoTracking().Where(x=>x.EntryId==id).OrderByDescending(x=>x.Version).ToListAsync(ct);
 public static string? Validate(FooterRequest r)
 {
  if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Trim().Length>100)return "Enter an internal name of 1–100 characters.";
  if(!new[]{"section","text","link","email","phone","social","copyright"}.Contains(r.Kind))return "Unknown footer entry type.";
  if(r.Text is null||r.Text.Length>4000||(r.Kind!="section"&&string.IsNullOrWhiteSpace(r.Text)))return "Enter display text of at most 4000 characters.";
  if(r.Url is null||r.Url.Length>2048||r.Url.Any(char.IsControl)||r.Icon is null)return "Invalid link.";
  if(r.Kind=="section"&&r.ParentId is not null)return "Sections cannot be nested.";
  if(r.Kind!="section"&&r.ParentId is null)return "Choose a section.";
  if((r.Kind is "link" or "social")&&!NavigationAdminService.SafeHref(r.Url))return "Use an internal path or HTTP/HTTPS URL.";
  if(r.Kind=="email"&&(!System.Net.Mail.MailAddress.TryCreate(r.Url,out var mail)||mail.Address!=r.Url))return "Enter an email address without mailto:.";
  if(r.Kind=="phone"&&!Regex.IsMatch(r.Url,@"^\+?[0-9 ()-]{5,30}$"))return "Enter a phone number without tel:.";
  if(r.Icon!=""&&!new[]{"facebook","instagram","youtube","discord","twitch","twitter","github"}.Contains(r.Icon))return "Unsupported social icon.";
  return null;
 }
 private async Task<string?> Relations(Guid id,FooterRequest r,CancellationToken ct)
 {
  if(r.Kind!="section"&&await db.FooterEntries.AnyAsync(x=>x.ParentId==id&&!x.IsDeleted,ct))return "Move or delete the section's entries first.";
  if(r.ParentId==id)return "An entry cannot contain itself.";
  if(r.ParentId.HasValue&&!await db.FooterEntries.AnyAsync(x=>x.Id==r.ParentId&&x.Kind=="section"&&!x.IsDeleted&&!x.IsArchived,ct))return "Choose an active section.";
  if(await db.FooterEntries.AnyAsync(x=>x.Id!=id&&x.ParentId==r.ParentId&&!x.IsDeleted&&x.Name.ToLower()==r.Name.Trim().ToLower(),ct))return "This internal name is already used in the section.";
  return null;
 }
 public async Task<FooterResult> Save(Guid? id,FooterRequest r,string actor,CancellationToken ct)
 {
  var error=Validate(r);if(error is not null)return new(400,error);
  await using var tx=await new GameDataAssignmentService(db).BeginAsync(ct);
  var entry=id.HasValue?await db.FooterEntries.SingleOrDefaultAsync(x=>x.Id==id,ct):new FooterEntry();
  if(entry is null)return new(404,"Footer entry not found.");
  if(id.HasValue&&entry.Version!=r.Version)return new(409,"Entry changed. Reload before saving.");
  if(entry.IsDeleted||entry.IsArchived)return new(409,"Restore or unarchive before editing.");
  error=await Relations(entry.Id,r,ct);if(error is not null)return new(409,error);
  Apply(entry,r);if(id.HasValue)entry.Version++;else db.FooterEntries.Add(entry);
  Record(entry,id.HasValue?"updated":"created",actor);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(200,Entry:entry);
 }
 public async Task<FooterResult> Change(Guid id,int version,string action,Guid? revisionId,string actor,CancellationToken ct)
 {
  await using var tx=await new GameDataAssignmentService(db).BeginAsync(ct);var entry=await db.FooterEntries.SingleOrDefaultAsync(x=>x.Id==id,ct);
  if(entry is null)return new(404,"Footer entry not found.");if(entry.Version!=version)return new(409,"Entry changed. Reload before continuing.");
  if(action=="restore")
  {
   var revision=await db.FooterRevisions.SingleOrDefaultAsync(x=>x.EntryId==id&&x.Id==revisionId,ct);if(revision is null)return new(404,"Revision not found.");
   var previous=JsonSerializer.Deserialize<FooterEntry>(revision.Snapshot)!;if(previous.IsDeleted)return new(400,"Select a revision before deletion.");
   var r=new FooterRequest(previous.Name,previous.Kind,previous.Text,previous.Url,previous.Icon,previous.ParentId,previous.SortOrder,previous.OpenNewTab,version);
   var error=Validate(r)??await Relations(id,r,ct);if(error is not null)return new(409,error);
   Apply(entry,r);entry.IsDeleted=false;entry.IsArchived=previous.IsArchived;
  }
  else
  {
   if(entry.IsDeleted)return new(409,"Restore the deleted entry first.");
   if(action=="delete") {if(await db.FooterEntries.AnyAsync(x=>x.ParentId==id&&!x.IsDeleted,ct))return new(409,"Move or delete the section's entries first.");entry.IsDeleted=true;entry.IsArchived=true;}
   else if(action=="archive")entry.IsArchived=true;
   else if(action=="unarchive") {var error=await Relations(id,new(entry.Name,entry.Kind,entry.Text,entry.Url,entry.Icon,entry.ParentId,entry.SortOrder,entry.OpenNewTab,version),ct);if(error is not null)return new(409,error);entry.IsArchived=false;}
   else return new(400,"Unknown action.");
  }
  entry.Version++;Record(entry,action,actor);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(200,Entry:entry);
 }
 private static void Apply(FooterEntry e,FooterRequest r) {e.Name=r.Name.Trim();e.Kind=r.Kind;e.Text=r.Text;e.Url=(r.Kind is "link" or "social" or "email" or "phone")?r.Url.Trim():"";e.Icon=r.Kind=="social"?r.Icon:"";e.ParentId=r.ParentId;e.SortOrder=r.SortOrder;e.OpenNewTab=r.OpenNewTab;}
 private void Record(FooterEntry e,string action,string actor)=>db.FooterRevisions.Add(new(){EntryId=e.Id,Version=e.Version,Action=action,Actor=actor[..Math.Min(actor.Length,100)],Snapshot=JsonSerializer.Serialize(e)});
}
