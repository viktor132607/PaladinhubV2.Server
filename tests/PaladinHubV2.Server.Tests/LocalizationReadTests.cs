using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Tests;
public sealed class LocalizationReadTests
{
    [Fact]
    public async Task Resources_FallBackToEnglish_AndExcludeArchivedLanguages()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.SiteLanguages.AddRange(new SiteLanguage { Code = "en", Name = "English", ResourcesJson = "{\"Home\":\"Home\",\"Login\":\"Sign in\"}" }, new SiteLanguage { Code = "bg", Name = "Български", ResourcesJson = "{\"Home\":\"Начало\"}" }, new SiteLanguage { Code = "de", Name = "Deutsch", IsArchived = true, ResourcesJson = "{\"Home\":\"Start\"}" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new PublicLocalizationController(db);
        var result = Assert.IsType<OkObjectResult>(await controller.Resources("BG", TestContext.Current.CancellationToken));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal("bg", json.GetProperty("code").GetString());
        Assert.Equal("Начало", json.GetProperty("translations").GetProperty("Home").GetString());
        Assert.Equal("Sign in", json.GetProperty("translations").GetProperty("Login").GetString());
        foreach (var code in new[] { "de", "missing" })
        {
            result = Assert.IsType<OkObjectResult>(await controller.Resources(code, TestContext.Current.CancellationToken));
            json = JsonSerializer.SerializeToElement(result.Value);
            Assert.Equal("en", json.GetProperty("code").GetString());
            Assert.Equal("Home", json.GetProperty("translations").GetProperty("Home").GetString());
        }
    }
}
