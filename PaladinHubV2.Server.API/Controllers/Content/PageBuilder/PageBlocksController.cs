using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Route("api/blocks")]
	public class PageBlocksController : ControllerBase
	{
		private readonly IBlockRenderer _renderer;
		public PageBlocksController(IBlockRenderer renderer) => _renderer = renderer;

		// Рендерира ЕДИН блок – точно това вика preview-то
		[HttpPost("render")]
		public async Task<IActionResult> Render([FromBody] JsonElement blockJson)
		{
			// BlockRenderer очаква масив; увиваме блока в масив.
			var oneBlockLayout = $"[{blockJson.GetRawText()}]";
			var html = await _renderer.RenderAsync(oneBlockLayout);
			return Content(html, "text/html");
		}

		// (по избор) – рендерира цял масив от блокове
		[HttpPost("render-layout")]
		public async Task<IActionResult> RenderLayout([FromBody] JsonElement layoutJson)
		{
			var html = await _renderer.RenderAsync(layoutJson.GetRawText());
			return Content(html, "text/html");
		}
	}
}
