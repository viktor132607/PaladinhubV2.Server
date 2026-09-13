using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Controllers.Content;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Footer;
namespace PaladinHubV2.Server.Tests;
public sealed class FooterTests
{
 private static FooterRequest Request(string kind,string url)=>new("Contact",kind,"Label",url,"",Guid.NewGuid(),0,false,1);
 [Theory]
 [InlineData("link","/Home/Home",true)]
 [InlineData("link","https://example.com",true)]
 [InlineData("link","javascript:alert(1)",false)]
 [InlineData("link","/\\evil.test",false)]
 [InlineData("email","mail@example.com",true)]
 [InlineData("email","mailto:mail@example.com",false)]
 [InlineData("phone","+359 888 123456",true)]
 [InlineData("phone","tel:+359888123456",false)]
 public void ValidatesTypedLinks(string kind,string url,bool valid)=>Assert.Equal(valid,FooterService.Validate(Request(kind,url)) is null);
 [Fact]public void RequiresASectionAndForbidsNestedSections()
 {
  Assert.NotNull(FooterService.Validate(Request("link","/Home") with {ParentId=null}));
  Assert.NotNull(FooterService.Validate(Request("section","") ));
  Assert.Null(FooterService.Validate(Request("section","") with {ParentId=null,Text=""}));
 }
 [Fact]public async Task InvalidCreateDoesNotTouchDatabase()
 {
  await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  var controller=new FooterController(db){ControllerContext=new ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
  var result=Assert.IsType<ObjectResult>(await controller.Create(Request("link","javascript:bad"),TestContext.Current.CancellationToken));
  Assert.Equal(400,result.StatusCode);
  Assert.Empty(db.FooterEntries);
 }
 [Fact]public async Task PublicReadHidesArchivedSectionsAndTheirChildren()
 {
  await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  var section=new FooterEntry{Name="Active",Kind="section"};var hidden=new FooterEntry{Name="Hidden",Kind="section",IsArchived=true};
  db.FooterEntries.AddRange(section,hidden,new FooterEntry{Name="Visible",ParentId=section.Id,Text="Visible"},new FooterEntry{Name="Hidden child",ParentId=hidden.Id},new FooterEntry{Name="Deleted",ParentId=section.Id,IsDeleted=true});await db.SaveChangesAsync(TestContext.Current.CancellationToken);
  var result=Assert.IsType<OkObjectResult>(await new PublicFooterController(db).Read(TestContext.Current.CancellationToken));var json=JsonSerializer.SerializeToElement(result.Value);Assert.Equal(2,json.GetArrayLength());Assert.DoesNotContain("Hidden",json.ToString());
 }
}
