using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/database")]
	public sealed class DatabaseController : ControllerBase
	{
		private readonly IAdminDatabaseService _database;

		public DatabaseController(IAdminDatabaseService database)
		{
			_database = database;
		}

		[HttpGet]
		public async Task<IActionResult> Index(
			[FromQuery] string? entity = "Spells",
			[FromQuery] string? search = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 20,
			CancellationToken cancellationToken = default)
		{
			var model = await _database.GetIndexAsync(
				entity,
				search,
				page,
				pageSize,
				cancellationToken);

			return Ok(model);
		}
	}
}
