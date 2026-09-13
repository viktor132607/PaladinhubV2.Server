using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Banners;
namespace PaladinHubV2.Server.API.Controllers.Content;

[ApiController,Authorize(Roles="Admin"),Route("Admin/api/banners")]
public sealed class BannersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]string? search,[FromQuery]string status="active",CancellationToken ct=default)
    {

        IEnumerable<BannerDto> items=await BannerStore.ReadAllAsync(db,ct);
        if(!string.IsNullOrWhiteSpace(search)){var n=search.Trim();items=items.Where(x=>x.InternalName.Contains(n,StringComparison.OrdinalIgnoreCase)||x.Title.Contains(n,StringComparison.OrdinalIgnoreCase)||x.Text.Contains(n,StringComparison.OrdinalIgnoreCase));}
        items=status.ToLowerInvariant() switch {"all"=>items,"archived"=>items.Where(x=>x.IsArchived&&!x.IsDeleted),"deleted"=>items.Where(x=>x.IsDeleted),"inactive"=>items.Where(x=>!x.IsDeleted&&!x.IsArchived&&!x.IsActive),_=>items.Where(x=>!x.IsDeleted&&!x.IsArchived&&x.IsActive)};
        return Ok(items.OrderBy(x=>x.Position).ThenBy(x=>x.SortOrder).ThenBy(x=>x.InternalName));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id,CancellationToken ct){return Ok(await BannerStore.HistoryAsync(db,id,ct));}

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BannerRequest request,CancellationToken ct)
    {
        var invalid=BannerStore.Validate(request,true);if(invalid is not null)return BadRequest(new{code="banner.validation",message=invalid});

        var result=await BannerStore.CreateAsync(db,request,User.Identity?.Name??"admin",ct);return Result(result,true);
    }

    [HttpPut("{id:guid}"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id,BannerRequest request,CancellationToken ct)
    {
        var invalid=BannerStore.Validate(request,false);if(invalid is not null)return BadRequest(new{code="banner.validation",message=invalid});
        return Result(await BannerStore.UpdateAsync(db,id,request,User.Identity?.Name??"admin",ct));
    }

    [HttpPost("{id:guid}/actions"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(Guid id,BannerActionRequest request,CancellationToken ct)
    {return Result(await BannerStore.ChangeAsync(db,id,request,User.Identity?.Name??"admin",ct));}

    private IActionResult Result(BannerStoreResult r,bool created=false)=>r.Status switch{200 when r.Banner is not null&&created=>Created($"/Admin/api/banners/{r.Banner.Id}",r.Banner),200 when r.Banner is not null=>Ok(r.Banner),404=>NotFound(new{code=r.Code,message=r.Message}),409=>Conflict(new{code=r.Code,message=r.Message}),_=>BadRequest(new{code=r.Code,message=r.Message})};
}

[ApiController,Route("api/banners")]
public sealed class PublicBannersController(AppDbContext db):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Visible([FromQuery]string path="/",CancellationToken ct=default)
    {
        path=BannerStore.NormalizePath(path);var now=DateTimeOffset.UtcNow;
        var scoped=(await BannerStore.ReadAllAsync(db,ct)).Where(x=>!x.IsDeleted&&!x.IsArchived&&x.IsActive).Where(x=>x.Pages.Count==0||x.Pages.Any(p=>string.Equals(BannerStore.NormalizePath(p),path,StringComparison.OrdinalIgnoreCase))).ToList();
        var visible=scoped.Where(x=>BannerStore.IsVisible(x,path,now)).OrderBy(x=>x.Position).ThenBy(x=>x.SortOrder).ThenBy(x=>x.Id).Select(x=>new{x.Id,x.Title,x.Text,x.ImageUrl,x.AltText,x.ButtonText,x.ButtonUrl,x.Kind,x.Position,x.StartAtUtc,x.EndAtUtc,x.SortOrder,x.IsDismissible,x.Version,translationKeys=new{title=$"banner.{x.Id:N}.title",text=$"banner.{x.Id:N}.text",alt=$"banner.{x.Id:N}.alt",button=$"banner.{x.Id:N}.button"}}).ToArray();
        var next=scoped.SelectMany(x=>new[]{x.StartAtUtc,x.EndAtUtc}).Where(x=>x.HasValue&&x.Value>now).Select(x=>x!.Value).OrderBy(x=>x).Cast<DateTimeOffset?>().FirstOrDefault();
        return Ok(new{items=visible,nextChangeAtUtc=next});
    }
}
