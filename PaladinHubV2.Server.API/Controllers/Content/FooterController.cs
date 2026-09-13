using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Footer;
namespace PaladinHubV2.Server.API.Controllers.Content;
[ApiController,Authorize(Roles="Admin"),Route("Admin/api/footer")]
public sealed class FooterController(AppDbContext db):ControllerBase
{
 private readonly FooterService footer=new(db);
 public sealed record ChangeRequest(int Version,string Action,Guid? RevisionId);
 [HttpGet]public async Task<IActionResult> List(CancellationToken ct)=>Ok(await footer.List(ct));
 [HttpGet("{id:guid}/history")]public async Task<IActionResult> History(Guid id,CancellationToken ct)=>Ok(await footer.History(id,ct));
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult> Create(FooterRequest r,CancellationToken ct)=>Respond(await footer.Save(null,r,User.Identity?.Name??"admin",ct));
 [HttpPut("{id:guid}"),ValidateAntiForgeryToken]public async Task<IActionResult> Update(Guid id,FooterRequest r,CancellationToken ct)=>Respond(await footer.Save(id,r,User.Identity?.Name??"admin",ct));
 [HttpPost("{id:guid}/actions"),ValidateAntiForgeryToken]public async Task<IActionResult> Change(Guid id,ChangeRequest r,CancellationToken ct)=>Respond(await footer.Change(id,r.Version,r.Action,r.RevisionId,User.Identity?.Name??"admin",ct));
 private IActionResult Respond(FooterResult r)=>r.Entry is not null?StatusCode(r.Status,r.Entry):StatusCode(r.Status,new{message=r.Message});
}
[ApiController,Route("api/footer")]
public sealed class PublicFooterController(AppDbContext db):ControllerBase
{
 [HttpGet] public async Task<IActionResult> Read(CancellationToken ct)=>Ok(await db.FooterEntries.AsNoTracking().Where(x=>!x.IsDeleted&&!x.IsArchived&&(x.Kind=="section"||db.FooterEntries.Any(p=>p.Id==x.ParentId&&p.Kind=="section"&&!p.IsDeleted&&!p.IsArchived))).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).Select(x=>new{x.Id,x.ParentId,x.Kind,x.Text,x.Url,x.Icon,x.OpenNewTab,x.SortOrder}).ToListAsync(ct));
}
