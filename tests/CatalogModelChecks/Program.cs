using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql("Host=127.0.0.1;Database=compile_only;Username=unused;Password=unused").Options);
var schema = db.Database.GenerateCreateScript();
foreach (var table in new[] { "MediaRevisions", "ItemRarities", "RarityRevisions", "GamePatches", "PatchRevisions", "GameTags", "TagRevisions", "GameDisciplines", "DisciplineRevisions" })
    if (!schema.Contains($"CREATE TABLE \"{table}\"")) throw new Exception($"Missing table: {table}");
if (!schema.Contains("\"TagIds\" integer[] NOT NULL")) throw new Exception("Tags must persist as a non-null array.");

// Compile the exact shapes used by filters/catalog counts without opening a network connection.
var queries = new[] {
    db.Items.Where(i => i.RarityId == 1).ToQueryString(),
    db.Spells.Where(s => s.PatchId == 1).ToQueryString(),
    db.Spells.Where(s => s.TagIds.Contains(1)).ToQueryString(),
    db.Items.Where(i => i.TagIds.Length == 0).ToQueryString(),
    db.Spells.Where(s => db.GameTags.Any(t => !t.IsDeleted && s.TagIds.Contains(t.Id) && t.Name.Contains("healing"))).ToQueryString(),
    db.GameTags.Select(t => new { t.Id, Count = db.Items.Count(i => i.TagIds.Contains(t.Id)) + db.Spells.Count(s => s.TagIds.Contains(t.Id)) }).ToQueryString()
};
if (queries.Any(string.IsNullOrWhiteSpace)) throw new Exception("A query did not translate.");
foreach (var type in new[] { typeof(CategoriesController), typeof(ClassesController), typeof(TagsController), typeof(PatchesController), typeof(RaritiesController) })
{
    if (type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().All(a => a.Roles != "Admin"))
        throw new Exception($"Missing admin authorization: {type.Name}");
    foreach (var method in new[] { "Create", "Edit", "Delete", "Restore" })
        if (!type.GetMethod(method)!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true))
            throw new Exception($"Missing CSRF validation: {type.Name}.{method}");
}
if (!db.Model.FindEntityType(typeof(Spell))!.FindProperty(nameof(Spell.DisciplineId))!.GetContainingForeignKeys().Any())
    throw new Exception("Class assignments require a foreign key.");
Console.WriteLine("PASS: catalog schema, tag array/search/count query translation, assignment FK, admin authorization and CSRF.");

foreach (var type in new[] { typeof(Spell), typeof(Item) })
    if (!db.Model.FindEntityType(type)!.FindProperty("PatchId")!.GetContainingForeignKeys().Any())
        throw new Exception("Patch assignments require a foreign key.");
Console.WriteLine("PASS: patch tables, filter translation, assignment foreign keys and authorization.");

if (!db.Model.FindEntityType(typeof(Item))!.FindProperty("RarityId")!.GetContainingForeignKeys().Any()) throw new Exception("Rarity FK missing");
Console.WriteLine("PASS: rarity schema, FK, query translation and authorization.");

foreach (var method in new[] { "Edit", "Delete", "Restore" })
    if (!typeof(MediaController).GetMethod(method)!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true)) throw new Exception("Media CSRF missing");
if (typeof(MediaController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().All(a => a.Roles != "Admin")) throw new Exception("Media authorization missing");
_ = db.SpellIcons.Select(i => new { size = i.Content.Length, count = db.Spells.Count(s => s.Icon != null && s.Icon.ToLower().Contains(i.Id.ToString())) }).ToQueryString();
Console.WriteLine("PASS: media schema, byte length/reference query translation and authorization.");
