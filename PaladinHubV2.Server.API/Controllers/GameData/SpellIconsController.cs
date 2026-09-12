using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("Admin/api/spells/icons")]
public sealed class SpellIconsController(AppDbContext db) : ControllerBase
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private sealed record IconEntry(string Name, string Icon, string Kind);

    [HttpGet]
    public async Task<IActionResult> Browse(string? search, int page = 1, int pageSize = 64, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var spells = await db.Spells.AsNoTracking()
            .Where(spell => spell.Icon != null && spell.Icon != "")
            .Select(spell => new { spell.Name, spell.Icon, spell.Quality })
            .ToListAsync(cancellationToken);
        var uploads = await db.SpellIcons.AsNoTracking().Where(icon => !icon.IsArchived && !icon.IsDeleted)
            .Select(icon => new { icon.Id, icon.Name }).ToListAsync(cancellationToken);
        var records = spells.Select(spell => new IconEntry(spell.Name, spell.Icon!, spell.Quality))
            .Concat(uploads.Select(icon => new IconEntry(icon.Name, $"/api/spell-icons/{icon.Id}", "upload")));
        var items = await db.Items.AsNoTracking().Select(i => new { i.Name, i.Icon, i.SecondIcon }).ToListAsync(cancellationToken);
        records = records.Concat(items.SelectMany(i => new[] { i.Icon, i.SecondIcon }.Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => new IconEntry(i.Name, v!.StartsWith("/") || v.Contains("://") ? v : "/images/ItemIcons/" + Uri.EscapeDataString(v), "item"))));
        if (!string.IsNullOrWhiteSpace(search))
            records = records.Where(icon => icon.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || icon.Icon.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        var hiddenIds = await db.SpellIcons.Where(i => i.IsArchived || i.IsDeleted).Select(i => i.Id.ToString()).ToListAsync(cancellationToken);
        records = records.Where(r => !hiddenIds.Any(id => r.Icon.Contains(id, StringComparison.OrdinalIgnoreCase)));
        var all = records.DistinctBy(icon => icon.Icon).OrderBy(icon => icon.Name).ThenBy(icon => icon.Icon).ToList();
        var pages = Math.Max(1, (int)Math.Ceiling(all.Count / (double)pageSize));
        page = Math.Min(page, pages);
        return Ok(new { page, pages, total = all.Count, icons = all.Skip((page - 1) * pageSize).Take(pageSize) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes + 65536)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxBytes)
            return BadRequest(new { message = "Choose a PNG, JPEG, GIF or WebP image up to 5 MB." });
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();
        var contentType = ImageType(content);
        if (contentType == null)
            return BadRequest(new { message = "Only PNG, JPEG, GIF and WebP images are supported." });
        var name = Path.GetFileName(file.FileName);
        var icon = new SpellIcon
        {
            Id = Guid.NewGuid(), Name = name.Length > 255 ? name[..255] : name,
            ContentType = contentType, Content = content, CreatedAtUtc = DateTime.UtcNow
        };
        await using var transaction = await CategoryRules.BeginAsync(db, cancellationToken);
        db.SpellIcons.Add(icon);
        MediaController.Record(db, icon, "uploaded", User);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { icon = $"/api/spell-icons/{icon.Id}", name = icon.Name });
    }

    [AllowAnonymous]
    [HttpGet("/api/spell-icons/{id:guid}")]
    public async Task<IActionResult> Image(Guid id, CancellationToken cancellationToken)
    {
        var icon = await db.SpellIcons.AsNoTracking().SingleOrDefaultAsync(image => image.Id == id, cancellationToken);
        if (icon == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return File(icon.Content, icon.ContentType);
    }

    private static string? ImageType(byte[] content)
    {
        var bytes = content.AsSpan();
        if (bytes.Length >= 24 && bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        if (bytes.Length >= 10 && (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8))) return "image/gif";
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
