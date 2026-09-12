using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/database")]
	public sealed class DatabaseController : ControllerBase
	{
		private readonly DatabaseBrowserService _browser;

		public DatabaseController(AppDbContext db)
		{
			_browser = new DatabaseBrowserService(db);
		}

		[HttpGet]
		public async Task<IActionResult> Index(
			[FromQuery] string? entity = "Spells",
			[FromQuery] string? search = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 20,
			[FromQuery] int? categoryId = null,
			[FromQuery] int? disciplineId = null,
			[FromQuery] int? tagId = null,
			[FromQuery] int? patchId = null,
			[FromQuery] int? rarityId = null,
			CancellationToken cancellationToken = default)
		{
			DatabaseBrowseResult result = await _browser.BrowseAsync(
				entity,
				search,
				page,
				pageSize,
				categoryId,
				disciplineId,
				tagId,
				patchId,
				rarityId,
				cancellationToken);

			return result.Error switch
			{
				DatabaseBrowseError.None => Ok(result.Model),
				DatabaseBrowseError.CategoryNotFound =>
					BadRequest(new
					{
						message = "Category does not exist."
					}),
				DatabaseBrowseError.DisciplineNotFound =>
					BadRequest(new
					{
						message =
							"Class or specialization does not exist."
					}),
				_ => BadRequest()
			};
		}
	}
}
