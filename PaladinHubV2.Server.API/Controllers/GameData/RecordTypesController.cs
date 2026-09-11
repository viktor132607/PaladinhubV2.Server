using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("Admin/api/record-types")]
public sealed class RecordTypesController(AppDbContext db) : ControllerBase
{
    public sealed record TypeRequest([property: Required, MaxLength(50)] string Name);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(
        await db.RecordTypes.AsNoTracking().OrderBy(type => type.Name)
            .Select(type => new { type.Name, usageCount = db.Spells.Count(spell => spell.Quality == type.Name) })
            .ToListAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TypeRequest request, CancellationToken cancellationToken)
    {
        var name = Normalize(request.Name);
        if (name.Length is 0 or > 50) return BadRequest(new { message = "Type must contain 1–50 characters." });
        db.RecordTypes.Add(new RecordType { Name = name });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        { return Conflict(new { message = "A type with this name already exists." }); }
        return Ok(new { name, usageCount = 0 });
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename([FromQuery] string name, TypeRequest request, CancellationToken cancellationToken)
    {
        var renamed = Normalize(request.Name);
        if (renamed.Length is 0 or > 50) return BadRequest(new { message = "Type must contain 1–50 characters." });
        try
        {
            // The FK cascades the rename to every record atomically, including concurrent edits.
            var count = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"RecordTypes\" SET \"Name\" = {renamed} WHERE \"Name\" = {name}", cancellationToken);
            if (count == 0) return NotFound(new { message = "Type no longer exists. Refresh the list." });
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        { return Conflict(new { message = "A type with this name already exists." }); }
        return Ok(new { name = renamed });
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromQuery] string name, [FromQuery] string? replacement, CancellationToken cancellationToken)
    {
        if (replacement == name) return BadRequest(new { message = "Choose a different replacement type." });
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Lock affected catalog rows in a stable order. Concurrent saves cannot introduce dangling types.
        var locked = await db.RecordTypes.FromSqlInterpolated(
            $"SELECT * FROM \"RecordTypes\" WHERE \"Name\" = {name} OR \"Name\" = {replacement} ORDER BY \"Name\" FOR UPDATE")
            .ToListAsync(cancellationToken);
        var type = locked.FirstOrDefault(current => current.Name == name);
        if (type is null) return NotFound(new { message = "Type no longer exists. Refresh the list." });
        if (replacement is not null && !locked.Any(current => current.Name == replacement))
            return BadRequest(new { message = "Replacement type does not exist." });
        if (replacement is null && await db.Spells.AnyAsync(spell => spell.Quality == name, cancellationToken))
            return Conflict(new { message = "This type is in use. Choose a replacement before deleting it." });
        if (replacement is not null)
            await db.Spells.Where(spell => spell.Quality == name)
                .ExecuteUpdateAsync(update => update.SetProperty(spell => spell.Quality, replacement), cancellationToken);
        db.RecordTypes.Remove(type);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();
}
