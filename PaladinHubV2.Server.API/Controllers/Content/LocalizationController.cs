using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Localization;
namespace PaladinHubV2.Server.API.Controllers.Content;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/languages")]
public sealed class LocalizationController(AppDbContext db) : ControllerBase
{
    private readonly LocalizationService languages = new(db);
    public sealed record ChangeRequest(int Version, string Action, Guid? RevisionId);
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await languages.List(ct));
    [HttpGet("{id:guid}/history")] public async Task<IActionResult> History(Guid id, CancellationToken ct) => Ok(await languages.History(id, ct));
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(2000000)]
    public async Task<IActionResult> Create(LanguageRequest request, CancellationToken ct) => Respond(await languages.Save(null, request, User.Identity?.Name ?? "admin", ct));
    [HttpPut("{id:guid}"), ValidateAntiForgeryToken, RequestSizeLimit(2000000)]
    public async Task<IActionResult> Update(Guid id, LanguageRequest request, CancellationToken ct) => Respond(await languages.Save(id, request, User.Identity?.Name ?? "admin", ct));
    [HttpPost("{id:guid}/actions"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(Guid id, ChangeRequest request, CancellationToken ct) => Respond(await languages.Change(id, request.Version, request.Action, request.RevisionId, User.Identity?.Name ?? "admin", ct));
    private IActionResult Respond(LanguageResult result) => result.Language is not null ? StatusCode(result.Status, result.Language) : StatusCode(result.Status, new { message = result.Message });
}

[ApiController, Route("api/localization")]
public sealed class PublicLocalizationController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Languages(CancellationToken ct) => Ok(await db.SiteLanguages.AsNoTracking().Where(l => !l.IsArchived && !l.IsDeleted).OrderBy(l => l.Code).Select(l => new { l.Code, l.Name }).ToListAsync(ct));
    [HttpGet("{code}")]
    public async Task<IActionResult> Resources(string code, CancellationToken ct)
    {
        code = code.ToLowerInvariant();
        var rows = await db.SiteLanguages.AsNoTracking().Where(l => !l.IsArchived && !l.IsDeleted && (l.Code == code || l.Code == "en")).ToListAsync(ct);
        var selected = rows.FirstOrDefault(l => l.Code == code) ?? rows.FirstOrDefault(l => l.Code == "en");
        var english = rows.FirstOrDefault(l => l.Code == "en");
        var resources = english is null ? new Dictionary<string,string>() : JsonSerializer.Deserialize<Dictionary<string,string>>(english.ResourcesJson)!;
        if (selected is not null) foreach (var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(selected.ResourcesJson)!) resources[pair.Key] = pair.Value;
        return Ok(new { code = selected?.Code ?? "en", translations = resources });
    }
}
