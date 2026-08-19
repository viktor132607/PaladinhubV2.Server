using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Domain.Services.Common;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.API.Controllers.Content.Talents
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("api/talents")]
	public sealed class TalentsApiController : ControllerBase
	{
		private readonly ITalentTreeAdminService _trees;

		public TalentsApiController(ITalentTreeAdminService trees)
		{
			_trees = trees;
		}

		[HttpPost("{key}")]
		public async Task<IActionResult> Save(
			[FromRoute] string key,
			[FromBody] SaveTreeRequest? request)
		{
			string? normalizedKey = key?.Trim();

			if (string.IsNullOrWhiteSpace(normalizedKey))
			{
				return BadRequest(new
				{
					message = "Talent tree key is required."
				});
			}

			if (request == null)
			{
				return BadRequest(new
				{
					message = "Talent tree data is required."
				});
			}

			string? requestKey = request.Key?.Trim();

			if (!string.Equals(
					normalizedKey,
					requestKey,
					StringComparison.OrdinalIgnoreCase))
			{
				return BadRequest(new
				{
					message =
						"The route key does not match the request key."
				});
			}

			OperationResult result =
				await _trees.SaveActiveStatesAsync(
					normalizedKey,
					request.Nodes);

			return result.Ok
				? NoContent()
				: BadRequest(new { message = result.Message });
		}
	}
}
