using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Route("api/blocks")]
	public sealed class PageBlocksController : ControllerBase
	{
		private readonly IBlockRenderer _renderer;

		public PageBlocksController(IBlockRenderer renderer)
		{
			_renderer = renderer;
		}

		[HttpPost("render")]
		public async Task<IActionResult> Render(
			[FromBody] JsonElement blockJson)
		{
			string html = await _renderer.RenderBlockAsync(
				blockJson.GetRawText());

			return Content(html, "text/html");
		}

		[HttpPost("render-layout")]
		public async Task<IActionResult> RenderLayout(
			[FromBody] JsonElement layoutJson)
		{
			string html = await _renderer.RenderAsync(
				layoutJson.GetRawText());

			return Content(html, "text/html");
		}
	}
}
