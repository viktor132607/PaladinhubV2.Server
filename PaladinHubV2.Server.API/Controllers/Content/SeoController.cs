using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Seo;

namespace PaladinHubV2.Server.API.Controllers.Content;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("Admin/api/seo")]
public sealed class SeoController : ControllerBase
{
    private readonly SeoService _seo;

    public SeoController(AppDbContext db)
    {
        _seo = new SeoService(db);
    }

    public sealed record ChangeRequest(
        int Version,
        string? Action,
        Guid? RevisionId);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        return Ok(await _seo.ListAsync(ct));
    }

    [HttpGet("targets")]
    public async Task<IActionResult> Targets(CancellationToken ct)
    {
        return Ok(new
        {
            registryVersion = SeoRouteRegistry.Version,
            staticRoutes = SeoRouteRegistry.StaticSeoTargets,
            pages = await _seo.ListPagesAsync(ct)
        });
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        return Ok(await _seo.HistoryAsync(id, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SeoRequest request,
        CancellationToken ct)
    {
        return Respond(await _seo.SaveAsync(null, request, Actor(), ct));
    }

    [HttpPut("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid id,
        SeoRequest request,
        CancellationToken ct)
    {
        return Respond(await _seo.SaveAsync(id, request, Actor(), ct));
    }

    [HttpPost("{id:guid}/actions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(
        Guid id,
        ChangeRequest request,
        CancellationToken ct)
    {
        return Respond(await _seo.ChangeAsync(
            id,
            request.Version,
            request.Action,
            request.RevisionId,
            Actor(),
            ct));
    }

    private IActionResult Respond(SeoResult result)
    {
        return result.Entry is not null
            ? StatusCode(result.Status, result.Entry)
            : StatusCode(result.Status, new { message = result.Message });
    }

    private string Actor()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ??
               User.Identity?.Name ??
               "admin";
    }
}

[ApiController]
[AllowAnonymous]
[Route("api/seo")]
public sealed class PublicSeoController : ControllerBase
{
    private readonly SeoService _seo;
    private readonly IConfiguration _configuration;

    public PublicSeoController(AppDbContext db, IConfiguration configuration)
    {
        _seo = new SeoService(db);
        _configuration = configuration;
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> Snapshot(CancellationToken ct)
    {
        string apiOrigin = _configuration["Api:PublicBaseUrl"] ??
                           $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        SeoPublicSnapshot snapshot = await _seo.GetPublicSnapshotAsync(
            _configuration["ClientApp:BaseUrl"],
            apiOrigin,
            ct);
        return Ok(snapshot);
    }
}
