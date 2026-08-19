using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Domain.Services.Paladin;

namespace PaladinHubV2.Server.API.Controllers.Content.Paladin
{
	[ApiController]
	[Route("api/paladin")]
	public sealed class PaladinController : ControllerBase
	{
		private const string CurrentSectionSessionKey = "current-section";
		private readonly IPaladinPageService _pages;

		public PaladinController(IPaladinPageService pages)
		{
			_pages = pages;
		}

		[AllowAnonymous]
		[HttpGet]
		public IActionResult Index()
		{
			return Ok(new { redirectUrl = "/Merchandise" });
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/overview")]
		public Task<IActionResult> Overview([FromRoute] string section)
			=> GetSectionPage(section, nameof(Overview));

		[AllowAnonymous]
		[HttpGet("{section:palsec}/gear")]
		public Task<IActionResult> Gear([FromRoute] string section)
			=> GetSectionPage(section, nameof(Gear));

		[AllowAnonymous]
		[HttpGet("{section:palsec}/stats")]
		public Task<IActionResult> Stats([FromRoute] string section)
			=> GetSectionPage(section, nameof(Stats));

		[AllowAnonymous]
		[HttpGet("{section:palsec}/rotation")]
		public Task<IActionResult> Rotation([FromRoute] string section)
			=> GetSectionPage(section, nameof(Rotation));

		[AllowAnonymous]
		[HttpGet("{section:palsec}/consumables")]
		public Task<IActionResult> Consumables([FromRoute] string section)
			=> GetSectionPage(section, nameof(Consumables));

		[AllowAnonymous]
		[HttpGet("{section:palsec}/talents")]
		public async Task<IActionResult> Talents([FromRoute] string section)
		{
			string normalizedSection = _pages.NormalizeSection(section);
			RememberSection(normalizedSection);
			CombinedViewModel model = await _pages.GetTalentsAsync(normalizedSection);
			return Ok(model);
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/{slug}")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Page(
			[FromRoute] string section,
			[FromRoute] string slug)
		{
			if (string.IsNullOrWhiteSpace(slug))
			{
				return BadRequest(new { message = "Page slug is required." });
			}

			string normalizedSection = _pages.NormalizeSection(section);
			PaladinContentPageResult? result = await _pages.GetContentPageAsync(
				normalizedSection,
				slug);

			if (result == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			RememberSection(normalizedSection);
			return Ok(new ContentPageResponse(
				result.Page,
				result.Html,
				User.IsInRole("Admin"),
				result.RenderError));
		}

		private async Task<IActionResult> GetSectionPage(
			string section,
			string actionName)
		{
			string normalizedSection = _pages.NormalizeSection(section);
			RememberSection(normalizedSection);
			CombinedViewModel model = await _pages.GetSectionPageAsync(
				normalizedSection,
				actionName);
			return Ok(model);
		}

		private void RememberSection(string normalizedSection)
		{
			HttpContext.Session.SetString(
				CurrentSectionSessionKey,
				normalizedSection);
		}

		public sealed record ContentPageResponse(
			ContentPageViewModel Page,
			string Html,
			bool CanEdit,
			string? RenderError);
	}
}
