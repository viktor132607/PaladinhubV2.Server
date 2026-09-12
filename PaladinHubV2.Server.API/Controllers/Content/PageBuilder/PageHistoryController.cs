using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder;

[ApiController, Authorize(Roles="Admin"), Route("Admin/api/page-history")]
public sealed class PageHistoryController(AppDbContext db, IJsonLayoutValidator validator) : ControllerBase
{
    private readonly PageLifecycleService pages = new(db,validator);
    public sealed record ChangeRequest(int Version, string Action, Guid? RevisionId);
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await pages.List(ct));
    [HttpGet("{id:int}")] public async Task<IActionResult> History(int id,CancellationToken ct) => Ok(await pages.History(id,ct));
    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(int id,ChangeRequest request,CancellationToken ct)
    {
        var result=await pages.Change(id,request.Version,request.Action,request.RevisionId,User.Identity?.Name??"admin",ct);
        return result.Status==204?NoContent():StatusCode(result.Status,new {message=result.Message});
    }
}
