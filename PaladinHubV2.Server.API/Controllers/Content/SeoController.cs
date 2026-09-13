using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Seo;
namespace PaladinHubV2.Server.API.Controllers.Content;
[ApiController,Authorize(Roles="Admin"),Route("Admin/api/seo")]
public sealed class SeoController(AppDbContext db):ControllerBase
{
 private readonly SeoService seo=new(db);public sealed record ChangeRequest(int Version,string Action,Guid? RevisionId);
 [HttpGet]public async Task<IActionResult> List(CancellationToken ct)=>Ok(await seo.List(ct));
 [HttpGet("pages")]public async Task<IActionResult> Pages(CancellationToken ct)=>Ok(await db.ContentPages.AsNoTracking().OrderBy(x=>x.Section).ThenBy(x=>x.Title).Select(x=>new{x.Id,x.Title,path="/"+x.Section+"/"+x.Slug,x.IsPublished}).ToListAsync(ct));
 [HttpGet("{id:guid}/history")]public async Task<IActionResult> History(Guid id,CancellationToken ct)=>Ok(await seo.History(id,ct));
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult> Create(SeoRequest r,CancellationToken ct)=>Respond(await seo.Save(null,r,User.Identity?.Name??"admin",ct));
 [HttpPut("{id:guid}"),ValidateAntiForgeryToken]public async Task<IActionResult> Update(Guid id,SeoRequest r,CancellationToken ct)=>Respond(await seo.Save(id,r,User.Identity?.Name??"admin",ct));
 [HttpPost("{id:guid}/actions"),ValidateAntiForgeryToken]public async Task<IActionResult> Change(Guid id,ChangeRequest r,CancellationToken ct)=>Respond(await seo.Change(id,r.Version,r.Action,r.RevisionId,User.Identity?.Name??"admin",ct));
 private IActionResult Respond(SeoResult r)=>r.Entry is not null?StatusCode(r.Status,r.Entry):StatusCode(r.Status,new{message=r.Message});
}
[ApiController,Route("api/seo")]
public sealed class PublicSeoController(AppDbContext db,IConfiguration configuration):ControllerBase
{
 [HttpGet("snapshot")]public async Task<IActionResult> Snapshot(CancellationToken ct)
 {
  var pages=await db.ContentPages.AsNoTracking().Where(x=>x.IsPublished).Select(x=>new{x.Id,x.Title,path="/"+x.Section+"/"+x.Slug}).ToListAsync(ct);
  var ids=pages.Select(x=>x.Id).ToArray();var entries=await db.SeoEntries.AsNoTracking().Where(x=>!x.IsDeleted&&!x.IsArchived&&(x.PageId==null||ids.Contains(x.PageId.Value))).ToListAsync(ct);
  return Ok(new{siteUrl=configuration["ClientApp:BaseUrl"],pages,entries=entries.Select(x=>new{x.Id,x.Version,path=x.PageId.HasValue?pages.Single(p=>p.Id==x.PageId).path:x.Path,x.Title,x.Description,x.CanonicalUrl,x.SocialTitle,x.SocialDescription,x.ImageUrl,x.Index,x.Follow})});
 }
}
