using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
namespace PaladinHubV2.Server.Domain.Services.Localization;

public sealed record LanguageRequest(string Code, string Name, Dictionary<string, string>? Translations, int Version);
public sealed record LanguageResult(int Status, string? Message = null, SiteLanguage? Language = null);
public sealed class LocalizationService(AppDbContext db)
{
    public Task<List<SiteLanguage>> List(CancellationToken ct) => db.SiteLanguages.AsNoTracking().OrderBy(l => l.Code).ToListAsync(ct);
    public Task<List<LanguageRevision>> History(Guid id, CancellationToken ct) => db.LanguageRevisions.AsNoTracking().Where(r => r.LanguageId == id).OrderByDescending(r => r.Version).ToListAsync(ct);
    public static string? Validate(LanguageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 35 || !Regex.IsMatch(request.Code, "^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8})*$")) return "Enter a language code such as en, bg or pt-BR.";
        try { _ = CultureInfo.GetCultureInfo(request.Code); } catch (CultureNotFoundException) { return "Unknown language code."; }
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100) return "Enter a language name of 1–100 characters.";
        if (request.Translations is null || request.Translations.Count > 5000) return "Provide at most 5000 translations.";
        if (request.Translations.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 500 || pair.Value is null || pair.Value.Length > 20000)) return "Keys must contain 1–500 characters and values at most 20000 characters.";
        if (JsonSerializer.Serialize(request.Translations).Length > 1000000) return "Translations must be under 1 MB.";
        return null;
    }
    public static bool CanHide(string code) => !string.Equals(code, "en", StringComparison.OrdinalIgnoreCase);
    public async Task<LanguageResult> Save(Guid? id, LanguageRequest request, string actor, CancellationToken ct)
    {
        var error = Validate(request); if (error is not null) return new(400, error);
        await using var tx = await new GameDataAssignmentService(db).BeginAsync(ct);
        var code = CultureInfo.GetCultureInfo(request.Code).Name.ToLowerInvariant();
        var language = id.HasValue ? await db.SiteLanguages.SingleOrDefaultAsync(l => l.Id == id, ct) : new SiteLanguage { Code = code };
        if (language is null) return new(404, "Language not found.");
        if (id.HasValue && language.Version != request.Version) return new(409, "Language changed. Reload before saving.");
        if (language.Code != code) return new(400, "Language codes cannot be changed. Create a new language instead.");
        if (language.IsArchived || language.IsDeleted) return new(409, "Restore or unarchive this language before editing.");
        if (await db.SiteLanguages.AnyAsync(l => l.Id != language.Id && l.Code == code, ct)) return new(409, "This language code already exists. Restore the existing language if deleted.");
        language.Name = request.Name.Trim(); language.ResourcesJson = JsonSerializer.Serialize(request.Translations);
        if (id.HasValue) language.Version++; else db.SiteLanguages.Add(language);
        Record(language, id.HasValue ? "updated" : "created", actor);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Language: language);
    }
    public async Task<LanguageResult> Change(Guid id, int version, string action, Guid? revisionId, string actor, CancellationToken ct)
    {
        await using var tx = await new GameDataAssignmentService(db).BeginAsync(ct);
        var language = await db.SiteLanguages.SingleOrDefaultAsync(l => l.Id == id, ct);
        if (language is null) return new(404, "Language not found.");
        if (language.Version != version) return new(409, "Language changed. Reload before continuing.");
        if (action == "restore")
        {
            var revision = await db.LanguageRevisions.SingleOrDefaultAsync(r => r.LanguageId == id && r.Id == revisionId, ct);
            if (revision is null) return new(404, "Revision not found.");
            var previous = JsonSerializer.Deserialize<SiteLanguage>(revision.Snapshot)!;
            if (previous.IsDeleted) return new(400, "Choose a revision before deletion.");
            var error = Validate(new(language.Code, previous.Name, JsonSerializer.Deserialize<Dictionary<string, string>>(previous.ResourcesJson), version));
            if (error is not null) return new(400, error);
            language.Name = previous.Name; language.ResourcesJson = previous.ResourcesJson;
            language.IsArchived = CanHide(language.Code) && previous.IsArchived; language.IsDeleted = false;
        }
        else
        {
            if (language.IsDeleted) return new(409, "Restore the deleted language first.");
            if ((action is "archive" or "delete") && !CanHide(language.Code)) return new(409, "English is the fallback language and cannot be archived or deleted.");
            if (action == "archive") language.IsArchived = true;
            else if (action == "unarchive") language.IsArchived = false;
            else if (action == "delete") { language.IsDeleted = true; language.IsArchived = true; }
            else return new(400, "Unknown action.");
        }
        language.Version++; Record(language, action, actor);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Language: language);
    }
    private void Record(SiteLanguage language, string action, string actor) => db.LanguageRevisions.Add(new()
    { LanguageId = language.Id, Version = language.Version, Action = action, Actor = actor[..Math.Min(actor.Length,100)], Snapshot = JsonSerializer.Serialize(language) });
}
