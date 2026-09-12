using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/content-templates")]
public sealed class ContentTemplatesController(AppDbContext db, IJsonLayoutValidator validator) : ControllerBase
{
    private readonly ContentTemplateService templates = new(db, validator);
    public sealed record ChangeRequest(int Version, string Action, Guid? RevisionId);
    [HttpGet] public async Task<IActionResult> List([FromQuery] string kind = "block", CancellationToken ct = default) => Ok(await templates.List(kind, ct));
    [HttpGet("{id:guid}/history")] public async Task<IActionResult> History(Guid id, CancellationToken ct) => Ok(await templates.History(id, ct));
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(600000)]
    public async Task<IActionResult> Create(TemplateRequest request, [FromQuery] string kind = "block", CancellationToken ct = default) => Respond(await templates.Save(null, kind, request, User.Identity?.Name ?? "admin", ct));
    [HttpPut("{id:guid}"), ValidateAntiForgeryToken, RequestSizeLimit(600000)]
    public async Task<IActionResult> Update(Guid id, TemplateRequest request, [FromQuery] string kind = "block", CancellationToken ct = default) => Respond(await templates.Save(id, kind, request, User.Identity?.Name ?? "admin", ct));
    [HttpPost("{id:guid}/actions"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(Guid id, ChangeRequest request, CancellationToken ct) => Respond(await templates.Change(id, request.Version, request.Action, request.RevisionId, User.Identity?.Name ?? "admin", ct));
    private IActionResult Respond(TemplateResult result) => result.Template is not null ? StatusCode(result.Status, result.Template) : StatusCode(result.Status, new { message = result.Message });
}
