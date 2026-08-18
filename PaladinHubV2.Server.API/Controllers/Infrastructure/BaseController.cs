using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models;
using PaladinHubV2.Server.Domain.Services.IService;
using PaladinHubV2.Server.Domain.Services.ItemsService;
using PaladinHubV2.Server.Domain.Services.SpellbookService;

namespace PaladinHubV2.Server.API.Controllers.Infrastructure
{
	public abstract class BaseController : ControllerBase
	{
		protected ISpellbookService SpellbookService { get; }

		protected IItemsService ItemsService { get; }

		protected ISectionService SectionService { get; }

		protected BaseController(
			ISpellbookService spellbookService,
			IItemsService itemsService,
			ISectionService sectionService)
		{
			SpellbookService = spellbookService;
			ItemsService = itemsService;
			SectionService = sectionService;
		}

		protected async Task<CombinedViewModel> BuildCombinedDataAsync(
			string? actionName = null)
		{
			actionName ??=
				RouteData.Values["action"]?.ToString() ??
				string.Empty;

			var spells =
				await SpellbookService.GetAllAsync();

			var items =
				await ItemsService.GetAllAsync();

			return new CombinedViewModel
			{
				Spells = spells,
				Items = items,
				PageTitle =
					SectionService.GetPageTitle(actionName),
				PageText =
					SectionService.GetPageText(actionName),
				CoverImage =
					SectionService.GetCoverImage(),
				CurrentSectionButtons =
					SectionService.GetCurrentSectionButtons(actionName),
				OtherSectionButtons =
					SectionService.GetOtherSectionButtons()
			};
		}

		protected async Task<IActionResult> CombinedDataAsync(
			string? actionName = null)
		{
			var model =
				await BuildCombinedDataAsync(actionName);

			return Ok(model);
		}
	}
}
