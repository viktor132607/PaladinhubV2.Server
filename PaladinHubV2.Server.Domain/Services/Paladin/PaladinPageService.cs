using PaladinHub.Models;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Domain.Services.ItemsService;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Domain.Services.SectionServices;
using PaladinHubV2.Server.Domain.Services.SpellbookService;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.Domain.Services.Paladin
{
	public sealed class PaladinPageService : IPaladinPageService
	{
		private readonly ISpellbookService _spellbookService;
		private readonly IItemsService _itemsService;
		private readonly HolySectionService _holyService;
		private readonly ProtectionSectionService _protectionService;
		private readonly RetributionSectionService _retributionService;
		private readonly IPageService _pages;
		private readonly ITalentTreeService _talentTrees;
		private readonly IBlockRenderer _blockRenderer;

		public PaladinPageService(
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

		public string NormalizeSection(string? section)
		{
			return section?.Trim().ToLowerInvariant() switch
			{
				"holy" => "holy",
				"protection" or "prot" => "protection",
				"retribution" or "retri" or "ret" => "retribution",
				_ => throw new ArgumentException(
					"Unsupported paladin section.",
					nameof(section))
			};
		}

		public Task<CombinedViewModel> GetSectionPageAsync(
			string normalizedSection,
			string actionName)
		{
			return BuildCombinedModel(normalizedSection, actionName);
		}

		public async Task<CombinedViewModel> GetTalentsAsync(string normalizedSection)
		{
			CombinedViewModel model = await BuildCombinedModel(
				normalizedSection,
				"Talents");

			model.TalentTrees = await _talentTrees.GetTalentTrees(
				normalizedSection,
				model.Spells);

			return model;
		}

		public async Task<PaladinContentPageResult?> GetContentPageAsync(
			string normalizedSection,
			string slug)
		{
			string normalizedSlug = slug.Trim().ToLowerInvariant();
			var page = await _pages.GetByRouteAsync(
				normalizedSection,
				normalizedSlug);

			if (page == null || !page.IsPublished)
			{
				return null;
			}

			BaseSectionService sectionService = ResolveSectionService(normalizedSection);
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
				RowVersionBase64 = Convert.ToBase64String(
					page.RowVersion ?? Array.Empty<byte>())
			};

			string html = string.Empty;
			string? renderError = null;

			try
			{
				html = await _blockRenderer.RenderAsync(page.JsonLayout);
			}
			catch (Exception exception)
			{
				renderError = exception.Message;
			}

			return new PaladinContentPageResult(model, html, renderError);
		}

		private async Task<CombinedViewModel> BuildCombinedModel(
			string normalizedSection,
			string actionName)
		{
			BaseSectionService sectionService = ResolveSectionService(normalizedSection);

			var spells = await _spellbookService.GetAllAsync();
			var items = await _itemsService.GetAllAsync();

			return new CombinedViewModel
			{
				Section = sectionService.ControllerName,
				Spells = spells,
				Items = items,
				PageTitle = sectionService.GetPageTitle(actionName),
				PageText = sectionService.GetPageText(actionName),
				CoverImage = sectionService.GetCoverImage(),
				CurrentSectionButtons = sectionService.GetCurrentSectionButtons(actionName),
				OtherSectionButtons = sectionService.GetOtherSectionButtons()
			};
		}

		private BaseSectionService ResolveSectionService(string normalizedSection)
		{
			return normalizedSection switch
			{
				"holy" => _holyService,
				"protection" => _protectionService,
				"retribution" => _retributionService,
				_ => throw new ArgumentOutOfRangeException(
					nameof(normalizedSection),
					normalizedSection,
					"Unsupported paladin section.")
			};
		}
	}
}
