using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/media")]
public sealed class MediaController(AppDbContext db) : ControllerBase
{
    public sealed record MediaRequest([property: Required, MaxLength(255)] string Name,
        [property: MaxLength(500)] string? AltText, [property: MaxLength(2000)] string? Description, bool IsArchived, int Version);
    public sealed record RestoreRequest(Guid RevisionId, int Version);
    public sealed record Snapshot(string Name, string AltText, string Description, bool IsArchived, bool IsDeleted);

    [HttpGet]
    public async Task<IActionResult> List(string? search, string status = "active", int page = 1, int pageSize = 32, CancellationToken ct = default)
    {
        var query = db.SpellIcons.AsNoTracking().AsQueryable();
        query = status switch { "all" => query, "deleted" => query.Where(i => i.IsDeleted), "archived" => query.Where(i => !i.IsDeleted && i.IsArchived), _ => query.Where(i => !i.IsDeleted && !i.IsArchived) };
        if (!string.IsNullOrWhiteSpace(search)) { var text = search.Trim().ToLower(); query = query.Where(i => i.Name.ToLower().Contains(text) || i.AltText.ToLower().Contains(text) || i.Description.ToLower().Contains(text)); }
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)); page = Math.Clamp(page, 1, pages);
        var media = await query.OrderByDescending(i => i.CreatedAtUtc).ThenBy(i => i.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new { i.Id, i.Name, i.AltText, i.Description, i.IsArchived, i.IsDeleted, i.Version, i.CreatedAtUtc, i.ContentType, size = i.Content.Length, icon = "/api/spell-icons/" + i.Id,
                usageCount = db.Spells.Count(s => s.Icon != null && s.Icon.ToLower().Contains(i.Id.ToString()))
                    + db.Items.Count(s => (s.Icon != null && s.Icon.ToLower().Contains(i.Id.ToString())) || (s.SecondIcon != null && s.SecondIcon.ToLower().Contains(i.Id.ToString())))
                    + db.ProductImages.Count(s => s.Url.ToLower().Contains(i.Id.ToString()))
                    + db.ContentPages.Count(s => s.JsonLayout.ToLower().Contains(i.Id.ToString()))
                    + db.DiscussionPosts.Count(s => s.Content.ToLower().Contains(i.Id.ToString()))
                    + db.DiscussionComments.Count(s => s.Content.ToLower().Contains(i.Id.ToString()))
            }).ToListAsync(ct);
        return Ok(new { media, page, pages, total });
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) => Ok(await db.MediaRevisions.AsNoTracking().Where(r => r.MediaId == id).OrderByDescending(r => r.Version).ToListAsync(ct));

    [HttpPut("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, MediaRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Name is required." });
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var media = await db.SpellIcons.SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);
        if (media is null) return NotFound(); if (media.Version != request.Version) return Stale();
        var action = media.IsArchived == request.IsArchived ? "updated" : request.IsArchived ? "archived" : "unarchived";
        media.Name = request.Name.Trim(); media.AltText = request.AltText?.Trim() ?? ""; media.Description = request.Description?.Trim() ?? ""; media.IsArchived = request.IsArchived; media.Version++;
        Record(db, media, action, User); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Ok(new { media.Id });
    }

    [HttpDelete("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, int version, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var media = await db.SpellIcons.SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);
        if (media is null) return NotFound(); if (media.Version != version) return Stale();
        if (await Usage(id, ct) > 0) return Conflict(new { message = "This image is in use. Remove its references or archive it." });
        media.IsDeleted = true; media.Version++; Record(db, media, "deleted", User);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return NoContent();
    }

    [HttpPost("{id:guid}/restore"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, RestoreRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var media = await db.SpellIcons.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (media is null) return NotFound(); if (media.Version != request.Version) return Stale();
        var revision = await db.MediaRevisions.SingleOrDefaultAsync(r => r.MediaId == id && r.Id == request.RevisionId, ct);
        if (revision is null) return NotFound();
        var snapshot = JsonSerializer.Deserialize<Snapshot>(revision.Snapshot)!;
        if (snapshot.IsDeleted) return BadRequest(new { message = "Select a revision before deletion." });
        media.Name = snapshot.Name; media.AltText = snapshot.AltText; media.Description = snapshot.Description; media.IsArchived = snapshot.IsArchived; media.IsDeleted = false; media.Version++;
        Record(db, media, "restored", User); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(new { media.Id });
    }

    private IActionResult Stale() => Conflict(new { message = "This image changed in another session. Refresh before saving." });
    private async Task<int> Usage(Guid id, CancellationToken ct)
    {
        var key = id.ToString();
        return await db.Spells.CountAsync(i => i.Icon != null && i.Icon.ToLower().Contains(key), ct)
            + await db.Items.CountAsync(i => (i.Icon != null && i.Icon.ToLower().Contains(key)) || (i.SecondIcon != null && i.SecondIcon.ToLower().Contains(key)), ct)
            + await db.ProductImages.CountAsync(i => i.Url.ToLower().Contains(key), ct)
            + await db.ContentPages.CountAsync(p => p.JsonLayout.ToLower().Contains(key), ct)
            + await db.DiscussionPosts.CountAsync(p => p.Content.ToLower().Contains(key), ct)
            + await db.DiscussionComments.CountAsync(p => p.Content.ToLower().Contains(key), ct);
    }

    internal static void Record(AppDbContext db, SpellIcon media, string action, ClaimsPrincipal user) => db.MediaRevisions.Add(new MediaRevision {
        MediaId = media.Id, Version = media.Version, Action = action, Actor = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin",
        Snapshot = JsonSerializer.Serialize(new Snapshot(media.Name, media.AltText, media.Description, media.IsArchived, media.IsDeleted))
    });

    internal static async Task<bool> CanAssign(AppDbContext db, string? value, string? previous, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value) || value == previous) return true;
        var marker = "/api/spell-icons/"; var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return true;
        var tail = value[(index + marker.Length)..].Split('?', '#')[0];
        return Guid.TryParse(tail, out var id) && await db.SpellIcons.AnyAsync(i => i.Id == id && !i.IsDeleted && !i.IsArchived, ct);
    }
}
