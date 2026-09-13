using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content;

public sealed class PublicLocalizationControllerTests
{
    [Fact]
    public async Task Languages_ReturnsOnlyActiveLanguagesOrderedByCode()
    {
        using AppDbContext db = CreateContext();
        db.SiteLanguages.AddRange(
            new SiteLanguage { Code = "en", Name = "English" },
            new SiteLanguage { Code = "bg", Name = "Bulgarian" },
            new SiteLanguage { Code = "fr", Name = "French", IsArchived = true },
            new SiteLanguage { Code = "de", Name = "German", IsDeleted = true });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new PublicLocalizationController(db);

        IActionResult result = await controller.Languages(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value)
            .Cast<object>()
            .ToList();
        Assert.Equal(new[] { "bg", "en" }, rows.Select(row => ControllerTestSupport.ReadString(row, "Code")));
        Assert.Equal(new[] { "Bulgarian", "English" }, rows.Select(row => ControllerTestSupport.ReadString(row, "Name")));
    }

    [Fact]
    public async Task Resources_RequestedLanguage_MergesEnglishFallbackAndOverrides()
    {
        using AppDbContext db = CreateContext();
        db.SiteLanguages.AddRange(
            new SiteLanguage
            {
                Code = "en",
                Name = "English",
                ResourcesJson = "{\"hello\":\"Hello\",\"englishOnly\":\"English only\"}"
            },
            new SiteLanguage
            {
                Code = "bg",
                Name = "Bulgarian",
                ResourcesJson = "{\"hello\":\"Здравей\",\"bulgarianOnly\":\"Само на български\"}"
            });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new PublicLocalizationController(db);

        IActionResult result = await controller.Resources("BG", TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("bg", ControllerTestSupport.ReadString(ok.Value, "code"));
        var translations = Assert.IsAssignableFrom<IDictionary<string, string>>(
            ControllerTestSupport.Read(ok.Value, "translations"));
        Assert.Equal("Здравей", translations["hello"]);
        Assert.Equal("English only", translations["englishOnly"]);
        Assert.Equal("Само на български", translations["bulgarianOnly"]);
    }

    [Fact]
    public async Task Resources_UnknownLanguage_FallsBackToEnglish()
    {
        using AppDbContext db = CreateContext();
        db.SiteLanguages.Add(new SiteLanguage
        {
            Code = "en",
            Name = "English",
            ResourcesJson = "{\"hello\":\"Hello\"}"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new PublicLocalizationController(db);

        IActionResult result = await controller.Resources("xx", TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("en", ControllerTestSupport.ReadString(ok.Value, "code"));
        var translations = Assert.IsAssignableFrom<IDictionary<string, string>>(
            ControllerTestSupport.Read(ok.Value, "translations"));
        Assert.Equal("Hello", translations["hello"]);
    }

    [Fact]
    public async Task Resources_NoAvailableLanguage_ReturnsEnglishCodeAndEmptyTranslations()
    {
        using AppDbContext db = CreateContext();
        var controller = new PublicLocalizationController(db);

        IActionResult result = await controller.Resources("bg", TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("en", ControllerTestSupport.ReadString(ok.Value, "code"));
        var translations = Assert.IsAssignableFrom<IDictionary<string, string>>(
            ControllerTestSupport.Read(ok.Value, "translations"));
        Assert.Empty(translations);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"public-localization-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
