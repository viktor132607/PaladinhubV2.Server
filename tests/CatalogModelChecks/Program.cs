using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql("Host=127.0.0.1;Database=compile_only;Username=unused;Password=unused").Options);
var schema = db.Database.GenerateCreateScript();
foreach (var table in new[] { "NavigationLinks", "NavigationRevisions", "MediaRevisions", "ItemRarities", "RarityRevisions", "GamePatches", "PatchRevisions", "GameTags", "TagRevisions", "GameDisciplines", "DisciplineRevisions" })
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
foreach (var type in new[] { typeof(CategoriesController), typeof(ClassesController), typeof(TagsController), typeof(PatchesController), typeof(RaritiesController), typeof(NavigationController) })
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

var safeHref = typeof(PaladinHubV2.Server.Domain.Services.Navigation.NavigationAdminService).GetMethod("SafeHref")!;
foreach (var href in new[] { "/Holy/Overview", "https://example.com/path", "http://example.com" })
    if (!(bool)safeHref.Invoke(null, new object?[] { href })!) throw new Exception("Valid link rejected");
foreach (var href in new[] { "javascript:alert(1)", "//evil.test", "/\\evil.test", "data:text/html,hi", "\nhttps://example.com", "" })
    if ((bool)safeHref.Invoke(null, new object?[] { href })!) throw new Exception("Unsafe link accepted");
Console.WriteLine("PASS: navigation schema, auth/CSRF, URL protocol and control-character validation.");

// Verify audit state transitions without opening a database connection.
using (var auditDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Database=unused;Username=unused").Options))
{
    var prepare = typeof(AppDbContext).GetMethod("UpdateContentPageRowVersions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
    var page = new ContentPage { Id=100, Section="holy",Slug="audit-check",Title="Original",JsonLayout="[]",IsPublished=true };
    auditDb.AuditActor="test-admin";auditDb.ContentPages.Add(page);prepare.Invoke(auditDb,null);
    var created=auditDb.ChangeTracker.Entries<PageRevision>().Single().Entity;
    if(created.Page!=page || created.Version!=1 || created.Actor!="test-admin" || page.RowVersion.Length!=16)throw new Exception("Page creation audit failed");
    auditDb.ChangeTracker.AcceptAllChanges();var oldVersion=page.RowVersion.ToArray();page.Title="Updated";prepare.Invoke(auditDb,null);
    if(page.Version!=2 || oldVersion.SequenceEqual(page.RowVersion) || auditDb.ChangeTracker.Entries<PageRevision>().Count()!=2)throw new Exception("Page update audit failed");
    auditDb.ChangeTracker.AcceptAllChanges();auditDb.ContentPages.Remove(page);prepare.Invoke(auditDb,null);
    if(auditDb.Entry(page).State!=EntityState.Modified || !page.IsDeleted || !page.IsArchived || page.IsPublished || page.Version!=3)throw new Exception("Recoverable page deletion failed");
    if(auditDb.Model.FindEntityType(typeof(ContentPage))!.GetQueryFilter() is null)throw new Exception("Page visibility filter missing");
}
Console.WriteLine("PASS: page create/update/delete audit, row versions, recovery snapshots and visibility filter.");

var templateService = new PaladinHubV2.Server.Domain.Services.PageBuilder.ContentTemplateService(db, new PaladinHubV2.Server.Domain.Services.PageBuilder.JsonLayoutValidator());
if (templateService.Validate("block", new("Example", "", "[{\"type\":\"paragraph\",\"Text\":\"Hello\"}]", 0)) is not null) throw new Exception("Valid block template rejected.");
foreach (var badJson in new[] { "[]", "{}", "not json", "[{\"type\":\"unknown\"}]", "[{\"type\":\"talenttree.dynamic\"}]" })
    if (templateService.Validate("block", new("Example", "", badJson, 0)) is null) throw new Exception("Invalid template accepted.");
var templatesController = typeof(PaladinHubV2.Server.API.Controllers.Content.PageBuilder.ContentTemplatesController);
if (templatesController.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().All(a => a.Roles != "Admin")) throw new Exception("Templates authorization missing.");
foreach (var method in new[] { "Create", "Update", "Change" })
    if (!templatesController.GetMethod(method)!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true)) throw new Exception("Templates CSRF missing.");
if (!db.Model.FindEntityType(typeof(ContentTemplate))!.FindProperty("Version")!.IsConcurrencyToken) throw new Exception("Template version must prevent lost updates.");
if (db.Model.FindEntityType(typeof(ContentTemplateRevision))!.GetForeignKeys().Single().DeleteBehavior != DeleteBehavior.Restrict) throw new Exception("Template history deletion must be restricted.");
Console.WriteLine("PASS: template validation, history FK, concurrency, admin authorization and CSRF.");
