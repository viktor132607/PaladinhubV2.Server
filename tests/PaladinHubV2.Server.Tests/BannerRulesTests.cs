using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Banners;
namespace PaladinHubV2.Server.Tests;
public sealed class BannerRulesTests
{
    private static BannerRequest Request() => new("notice","Title","Body",null,null,null,null,"information","above-content",[],null,null,0,true,true,1);
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.test")]
    [InlineData("/\\evil.test")]
    [InlineData("https://safe.test\n/path")]
    public void RejectsUnsafeUrls(string url) => Assert.NotNull(BannerStore.Validate(Request() with {ButtonUrl=url,ButtonText="Open"},true));
    [Theory]
    [InlineData("/Admin/Database")]
    [InlineData("/api/auth/me")]
    [InlineData("/Home/Home?private=true")]
    public void RejectsPrivateOrAmbiguousScopes(string path) => Assert.NotNull(BannerStore.Validate(Request() with {Pages=[path]},true));
    [Fact]
    public async Task CreateRejectsInvalidInputBeforeOpeningDatabase()
    {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var result=await new BannersController(db).Create(Request() with {Title=""},TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }
    [Fact]
    public void ScheduleUsesInclusiveStartExclusiveEndAndPublicState()
    {
        var now=DateTimeOffset.UtcNow;
        var banner=new BannerDto(Guid.NewGuid(),"notice","Title","Body",null,"",null,null,"information","above-content",["/Holy/Overview"],now,now.AddMinutes(1),0,true,true,false,false,1,now,now);
        Assert.True(BannerStore.IsVisible(banner,"/holy/overview/",now));
        Assert.False(BannerStore.IsVisible(banner,"/Holy/Overview",now.AddMinutes(-1)));
        Assert.False(BannerStore.IsVisible(banner,"/Holy/Overview",now.AddMinutes(1)));
        Assert.False(BannerStore.IsVisible(banner,"/Protection/Overview",now));
        Assert.False(BannerStore.IsVisible(banner with {IsArchived=true},"/Holy/Overview",now));
        Assert.False(BannerStore.IsVisible(banner with {IsDeleted=true},"/Holy/Overview",now));
        Assert.False(BannerStore.IsVisible(banner with {IsActive=false},"/Holy/Overview",now));
    }
}
