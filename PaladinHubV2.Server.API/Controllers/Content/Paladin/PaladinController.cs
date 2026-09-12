using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models;
using PaladinHubV2.Server.Domain.Services.SectionServices;

namespace PaladinHubV2.Server.API.Controllers.Content.Paladin
{
	[ApiController]
	[Route("api/paladin")]
	public sealed class PaladinController : ControllerBase
	{
		private const string CurrentSectionSessionKey =
			"current-section";

		private readonly PaladinContentService _content;

		public PaladinController(
			PaladinContentService content)
		{
			_content = content;
		}

		[AllowAnonymous]
		[HttpGet]
		public IActionResult Index()
		{
			return Ok(new
			{
				redirectUrl = "/Merchandise"
			});
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/overview")]
		public Task<IActionResult> Overview(
			[FromRoute] string section)
		{
			return GetSectionPage(
				section,
				nameof(Overview));
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/gear")]
		public Task<IActionResult> Gear(
			[FromRoute] string section)
		{
			return GetSectionPage(
				section,
				nameof(Gear));
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/stats")]
		public Task<IActionResult> Stats(
			[FromRoute] string section)
		{
			return GetSectionPage(
				section,
				nameof(Stats));
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/rotation")]
		public Task<IActionResult> Rotation(
			[FromRoute] string section)
		{
			return GetSectionPage(
				section,
				nameof(Rotation));
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/consumables")]
		public Task<IActionResult> Consumables(
			[FromRoute] string section)
		{
			return GetSectionPage(
				section,
				nameof(Consumables));
		}

		[AllowAnonymous]
		[HttpGet("{section:palsec}/talents")]
		public async Task<IActionResult> Talents(
			[FromRoute] string section)
		{
			string normalizedSection =
				PaladinContentService.NormalizeSection(section);

			RememberSection(normalizedSection);

			CombinedViewModel model =
				await _content.BuildTalentsModelAsync(
					normalizedSection);

			return Ok(model);
		}

		private async Task<IActionResult> GetSectionPage(
			string section,
			string actionName)
		{
			string normalizedSection =
				PaladinContentService.NormalizeSection(section);

			RememberSection(normalizedSection);

			CombinedViewModel model =
				await _content.BuildSectionModelAsync(
					normalizedSection,
					actionName);

			return Ok(model);
		}

		private void RememberSection(
			string normalizedSection)
		{
			HttpContext.Session.SetString(
				CurrentSectionSessionKey,
				normalizedSection);
		}
	}
}
