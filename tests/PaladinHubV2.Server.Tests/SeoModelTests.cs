using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Tests;

public sealed class SeoModelTests
{
    [Fact]
    public void SeoModelMatchesConcurrencyAndRestrictDeleteContract()
    {
        using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=127.0.0.1;Database=compile_only;Username=unused;Password=unused")
                .Options);

        var entry = db.Model.FindEntityType(typeof(SeoEntry))!;
        var revision = db.Model.FindEntityType(typeof(SeoRevision))!;

        Assert.True(entry.FindProperty(nameof(SeoEntry.Version))!.IsConcurrencyToken);
        Assert.Equal(2, entry.GetForeignKeys().Count());
        Assert.All(
            entry.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        var revisionForeignKey = Assert.Single(revision.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, revisionForeignKey.DeleteBehavior);
        Assert.Contains(
            revision.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Select(property => property.Name)
                         .SequenceEqual([nameof(SeoRevision.EntryId), nameof(SeoRevision.Version)]));
    }

    [Fact]
    public void SeoAdminControllerRequiresAdminAndCsrfForMutations()
    {
        Type controller = typeof(SeoController);
        Assert.Contains(
            controller.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>(),
            attribute => attribute.Roles == "Admin");

        foreach (string method in new[] { "Create", "Update", "Change" })
        {
            Assert.True(controller.GetMethod(method)!
                .IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true));
        }
    }

    [Fact]
    public void SeoPublicControllerIsExplicitlyAnonymous()
    {
        Assert.True(typeof(PublicSeoController)
            .IsDefined(typeof(AllowAnonymousAttribute), true));
    }
}
