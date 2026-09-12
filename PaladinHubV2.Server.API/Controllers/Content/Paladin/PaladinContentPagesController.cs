using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Domain.Services.SectionServices;

namespace PaladinHubV2.Server.API.Controllers.Content.Paladin
{
	[ApiController]
	[Route("api/paladin")]
	public sealed class PaladinContentPagesController : ControllerBase
	{
		private const string CurrentSectionSessionKey =
			"current-section";

		private readonly PaladinContentService _content;

		public PaladinContentPagesController(
			PaladinContentService content)
		{
			_content = content;
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/{slug}")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Page(
			[FromRoute] string section,
			[FromRoute] string slug)
		{
			if (string.IsNullOrWhiteSpace(slug))
			{
				return BadRequest(new
				{
					message = "Page slug is required."
				});
			}

			string normalizedSection =
				PaladinContentService.NormalizeSection(section);

			string normalizedSlug =
				slug.Trim().ToLowerInvariant();

			PaladinContentPageData? data =
				await _content.GetDynamicPageAsync(
					normalizedSection,
					normalizedSlug);

			if (data == null)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			HttpContext.Session.SetString(
				CurrentSectionSessionKey,
				normalizedSection);

			return Ok(new ContentPageResponse(
				data.Page,
				data.Html,
				User.IsInRole("Admin"),
				data.RenderError));
		}
	}
}
