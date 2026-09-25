using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Banners;

namespace PaladinHubV2.Server.API.Controllers.Content;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("Admin/api/banners")]
public sealed class BannersController(
    BannerStoreService banners)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string status = "active",
        CancellationToken ct = default)
    {
        return Ok(
            await banners.ListAsync(
                search,
                status,
                ct));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(
        Guid id,
        CancellationToken ct)
    {
        return Ok(
            await banners.HistoryAsync(
                id,
                ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        BannerRequest request,
        CancellationToken ct)
    {
        BannerStoreResult result =
            await banners.CreateAsync(
                request,
                Actor(),
                ct);

        return Result(
            result,
            created: true);
    }

    [HttpPut("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid id,
        BannerRequest request,
        CancellationToken ct)
    {
        return Result(
            await banners.UpdateAsync(
                id,
                request,
                Actor(),
                ct));
    }

    [HttpPost("{id:guid}/actions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(
        Guid id,
        BannerActionRequest request,
        CancellationToken ct)
    {
        return Result(
            await banners.ChangeAsync(
                id,
                request,
                Actor(),
                ct));
    }

    private string Actor() =>
        User.Identity?.Name ?? "admin";

    private IActionResult Result(
        BannerStoreResult result,
        bool created = false)
    {
        return result.Status switch
        {
            200 when
                result.Banner is not null &&
                created =>
                Created(
                    $"/Admin/api/banners/{result.Banner.Id}",
                    result.Banner),

            200 when
                result.Banner is not null =>
                Ok(result.Banner),

            404 =>
                NotFound(
                    new
                    {
                        code = result.Code,
                        message = result.Message
                    }),

            409 =>
                Conflict(
                    new
                    {
                        code = result.Code,
                        message = result.Message
                    }),

            _ =>
                BadRequest(
                    new
                    {
                        code = result.Code,
                        message = result.Message
                    })
        };
    }
}

[ApiController]
[Route("api/banners")]
public sealed class PublicBannersController(
    BannerStoreService banners)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Visible(
        [FromQuery] string path = "/",
        CancellationToken ct = default)
    {
        var result =
            await banners.VisibleAsync(
                path,
                ct);

        object[] items =
            result.Items
                .Select(item =>
                    new
                    {
                        item.Id,
                        item.Title,
                        item.Text,
                        item.ImageUrl,
                        item.AltText,
                        item.ButtonText,
                        item.ButtonUrl,
                        item.Kind,
                        item.Position,
                        item.StartAtUtc,
                        item.EndAtUtc,
                        item.SortOrder,
                        item.IsDismissible,
                        item.Version,
                        translationKeys =
                            new
                            {
                                title =
                                    $"banner.{item.Id:N}.title",
                                text =
                                    $"banner.{item.Id:N}.text",
                                alt =
                                    $"banner.{item.Id:N}.alt",
                                button =
                                    $"banner.{item.Id:N}.button"
                            }
                    })
                .Cast<object>()
                .ToArray();

        return Ok(
            new
            {
                items,
                nextChangeAtUtc =
                    result.NextChangeAtUtc
            });
    }
}
