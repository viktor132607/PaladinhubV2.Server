using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Tests.Support;

namespace PaladinHubV2.Server.Tests.Controllers.Content;

public sealed class PublicFooterControllerTests
{
    [Fact]
    public async Task Read_ReturnsActiveSectionsAndChildrenOfActiveSectionsOrderedBySortOrder()
    {
        using AppDbContext db = CreateContext();
        Guid firstSectionId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid secondSectionId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Guid archivedSectionId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        Guid deletedSectionId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        Guid childId = Guid.Parse("00000000-0000-0000-0000-000000000005");

        db.FooterEntries.AddRange(
            new FooterEntry { Id = firstSectionId, Kind = "section", Text = "First", SortOrder = 2 },
            new FooterEntry { Id = secondSectionId, Kind = "section", Text = "Second", SortOrder = 1 },
            new FooterEntry { Id = childId, ParentId = firstSectionId, Kind = "link", Text = "Child", SortOrder = 3 },
            new FooterEntry { Id = Guid.NewGuid(), ParentId = Guid.NewGuid(), Kind = "link", Text = "Orphan", SortOrder = 0 },
            new FooterEntry { Id = archivedSectionId, Kind = "section", Text = "Archived", SortOrder = 4, IsArchived = true },
            new FooterEntry { Id = Guid.NewGuid(), ParentId = archivedSectionId, Kind = "link", Text = "Archived child", SortOrder = 5 },
            new FooterEntry { Id = deletedSectionId, Kind = "section", Text = "Deleted", SortOrder = 6, IsDeleted = true },
            new FooterEntry { Id = Guid.NewGuid(), ParentId = deletedSectionId, Kind = "link", Text = "Deleted child", SortOrder = 7 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new PublicFooterController(db);

        IActionResult result = await controller.Read(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value)
            .Cast<object>()
            .ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { secondSectionId, firstSectionId, childId },
            rows.Select(row => Assert.IsType<Guid>(ControllerTestSupport.Read(row, "Id"))));
        Assert.Equal(new[] { "Second", "First", "Child" },
            rows.Select(row => ControllerTestSupport.ReadString(row, "Text")));
    }

    [Fact]
    public async Task Read_EmptyDatabase_ReturnsEmptyCollection()
    {
        using AppDbContext db = CreateContext();
        var controller = new PublicFooterController(db);

        IActionResult result = await controller.Read(TestContext.Current.CancellationToken);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value)
            .Cast<object>()
            .ToList();
        Assert.Empty(rows);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"public-footer-controller-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
