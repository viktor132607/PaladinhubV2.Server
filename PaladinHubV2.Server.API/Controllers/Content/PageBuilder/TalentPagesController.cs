using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder;

[ApiController, Authorize(Roles = "Admin")]
[Route("Admin/api/talent-pages")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TalentPagesController(AppDbContext db, IJsonLayoutValidator validator, IPageService pages) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.ContentPages.AsNoTracking()
        .Where(p => p.JsonLayout.Contains("talenttree.dynamic"))
        .OrderBy(p => p.Section).ThenBy(p => p.Title)
        .Select(p => new { id=p.Id, title=p.Title, section=p.Section, slug=p.Slug }).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var page = await pages.GetByIdAsync(id);
        return page == null ? NotFound() : Ok(Details(page));
    }
    private static object Details(ContentPage p) => new { id=p.Id,title=p.Title,section=p.Section,slug=p.Slug,
        jsonLayout=p.JsonLayout,rowVersionBase64=Convert.ToBase64String(p.RowVersion ?? Array.Empty<byte>()) };

    public sealed class SaveRequest
    {
        [Required] public string JsonLayout {get;init;} = "[]";
        public string? RowVersionBase64 {get;init;}
        [StringLength(200)] public string? Title {get;init;}
        public string? Section {get;init;}
        [StringLength(100)] public string? Slug {get;init;}
    }
    private void ValidateLayout(string json)
    {
        validator.ValidateOrThrow(json);
        using var doc=JsonDocument.Parse(json);
        var ids=new HashSet<string>();
        foreach(var b in doc.RootElement.EnumerateArray())
            if(b.GetProperty("type").GetString()=="talenttree.dynamic" && !ids.Add(b.GetProperty("id").GetString()!))
                throw new JsonLayoutValidationException(new[]{"Dynamic tree IDs must be unique."});
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaveRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Title))return BadRequest(new {message="Page title is required."});
        var section=(request.Section??"").ToLowerInvariant();
        if(section is not ("holy" or "protection" or "retribution"))return BadRequest(new {message="Invalid section."});
        try {ValidateLayout(request.JsonLayout);}catch(JsonLayoutValidationException ex){return BadRequest(new {message=ex.Message,errors=ex.Errors});}
        var slug=Regex.Replace((string.IsNullOrWhiteSpace(request.Slug)?request.Title:request.Slug).Trim().ToLowerInvariant(),@"[^\p{L}\p{Nd}]+","-").Trim('-');
        if(slug.Length is 0 or >100 || new[]{"overview","gear","talents","consumables","rotation","stats"}.Contains(slug))
            return BadRequest(new {message="Choose a unique slug, different from the existing guide pages."});
        if(await db.ContentPages.AnyAsync(p=>p.Section==section&&p.Slug==slug))return Conflict(new {message="Slug already exists."});
        var page=new ContentPage{Title=request.Title.Trim(),Section=section,Slug=slug,JsonLayout=request.JsonLayout,IsPublished=true,CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow,UpdatedBy=User.Identity?.Name};
        db.ContentPages.Add(page);await db.SaveChangesAsync();
        return Created($"/Admin/api/talent-pages/{page.Id}",Details(page));
    }
    [HttpPut("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id,SaveRequest request)
    {
        byte[] version;
        try {version=Convert.FromBase64String(request.RowVersionBase64??"");}catch(FormatException){return BadRequest(new {message="Invalid page version."});}
        if(version.Length==0)return BadRequest(new {message="Page version is required."});
        if(await pages.GetByIdAsync(id)==null)return NotFound();
        try
        {
            ValidateLayout(request.JsonLayout);
            var (saved,next)=await pages.UpdateLayoutSafeAsync(id,request.JsonLayout,version,User.Identity?.Name??"Admin");
            if(!saved||next==null)return Conflict(new {message="The page was modified. Reload before saving."});
            return Ok(new {id,rowVersionBase64=Convert.ToBase64String(next)});
        }
        catch(JsonLayoutValidationException ex){return BadRequest(new {message=ex.Message,errors=ex.Errors});}
    }
}
