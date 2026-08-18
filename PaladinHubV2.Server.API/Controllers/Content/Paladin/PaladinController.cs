using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Domain.Services.ItemsService;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Domain.Services.SectionServices;
using PaladinHubV2.Server.Domain.Services.SpellbookService;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.API.Controllers.Content.Paladin
{
	[ApiController]
	[Route("api/paladin")]
	public sealed class PaladinController : ControllerBase
	{
		private const string CurrentSectionSessionKey =
			"current-section";

		private readonly ISpellbookService _spellbookService;
		private readonly IItemsService _itemsService;
		private readonly HolySectionService _holyService;
		private readonly ProtectionSectionService _protectionService;
		private readonly RetributionSectionService _retributionService;
		private readonly IPageService _pages;
		private readonly ITalentTreeService _talentTrees;
		private readonly IBlockRenderer _blockRenderer;

		public PaladinController(
			ISpellbookService spellbookService,
			IItemsService itemsService,
			HolySectionService holyService,
			ProtectionSectionService protectionService,
			RetributionSectionService retributionService,
			IPageService pages,
			ITalentTreeService talentTrees,
			IBlockRenderer blockRenderer)
		{
			_spellbookService = spellbookService;
			_itemsService = itemsService;
			_holyService = holyService;
			_protectionService = protectionService;
			_retributionService = retributionService;
			_pages = pages;
			_talentTrees = talentTrees;
			_blockRenderer = blockRenderer;
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
				NormalizeSection(section);

			CombinedViewModel model =
				await BuildCombinedModel(
					normalizedSection,
					nameof(Talents));

			model.TalentTrees =
				await _talentTrees.GetTalentTrees(
					normalizedSection,
					model.Spells);

			return Ok(model);
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
				NormalizeSection(section);

			string normalizedSlug =
				slug.Trim().ToLowerInvariant();

			var page = await _pages.GetByRouteAsync(
				normalizedSection,
				normalizedSlug);

			if (page == null || !page.IsPublished)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			RememberSection(normalizedSection);

			BaseSectionService sectionService =
				ResolveSectionService(normalizedSection);

			var model = new ContentPageViewModel
			{
				Id = page.Id,
				Section = sectionService.ControllerName,
				Slug = page.Slug,
				Title = page.Title,
				JsonLayout = page.JsonLayout,
				IsPublished = page.IsPublished,
				UpdatedAt = page.UpdatedAt,
				UpdatedBy = page.UpdatedBy,

				RowVersionBase64 =
					Convert.ToBase64String(
						page.RowVersion ??
						Array.Empty<byte>())
			};

			string html = string.Empty;
			string? renderError = null;

			try
			{
				html = await _blockRenderer.RenderAsync(
					page.JsonLayout);
			}
			catch (Exception exception)
			{
				renderError = exception.Message;
			}

			return Ok(new ContentPageResponse(
				model,
				html,
				User.IsInRole("Admin"),
				renderError));
		}

		private async Task<IActionResult> GetSectionPage(
			string section,
			string actionName)
		{
			string normalizedSection =
				NormalizeSection(section);

			CombinedViewModel model =
				await BuildCombinedModel(
					normalizedSection,
					actionName);

			return Ok(model);
		}

		private async Task<CombinedViewModel> BuildCombinedModel(
			string normalizedSection,
			string actionName)
		{
			RememberSection(normalizedSection);

			BaseSectionService sectionService =
				ResolveSectionService(normalizedSection);

			/*
			 * Извикванията са последователни нарочно.
			 * Двете услуги може да използват един и същ scoped
			 * AppDbContext, който не поддържа паралелни операции.
			 */
			var spells =
				await _spellbookService.GetAllAsync();

			var items =
				await _itemsService.GetAllAsync();

			return new CombinedViewModel
			{
				Section = sectionService.ControllerName,
				Spells = spells,
				Items = items,

				PageTitle =
					sectionService.GetPageTitle(actionName),

				PageText =
					sectionService.GetPageText(actionName),

				CoverImage =
					sectionService.GetCoverImage(),

				CurrentSectionButtons =
					sectionService.GetCurrentSectionButtons(
						actionName),

				OtherSectionButtons =
					sectionService.GetOtherSectionButtons()
			};
		}

		private BaseSectionService ResolveSectionService(
			string normalizedSection)
		{
			return normalizedSection switch
			{
				"holy" =>
					_holyService,

				"protection" =>
					_protectionService,

				"retribution" =>
					_retributionService,

				_ => throw new ArgumentOutOfRangeException(
					nameof(normalizedSection),
					normalizedSection,
					"Unsupported paladin section.")
			};
		}

		private void RememberSection(
			string normalizedSection)
		{
			HttpContext.Session.SetString(
				CurrentSectionSessionKey,
				normalizedSection);
		}

		private static string NormalizeSection(
			string? section)
		{
			return section?
				.Trim()
				.ToLowerInvariant() switch
			{
				"holy" =>
					"holy",

				"protection" or "prot" =>
					"protection",

				"retribution" or "retri" or "ret" =>
					"retribution",

				_ => throw new ArgumentException(
					"Unsupported paladin section.",
					nameof(section))
			};
		}

		public sealed record ContentPageResponse(
			ContentPageViewModel Page,
			string Html,
			bool CanEdit,
			string? RenderError);
	}
}
