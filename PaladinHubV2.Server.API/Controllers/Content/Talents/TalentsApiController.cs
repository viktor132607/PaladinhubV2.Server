using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.API.Controllers.Content.Talents
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("api/talents")]
	public sealed class TalentsApiController : ControllerBase
	{
		private readonly ITalentTreeService _trees;

		public TalentsApiController(ITalentTreeService trees)
		{
			_trees = trees;
		}

		[HttpPost("{key}")]
		public async Task<IActionResult> Save(
			[FromRoute] string key,
			[FromBody] SaveTreeRequest? request)
		{
			TalentTreeSaveResult result =
				await _trees.ValidateAndSaveActiveStatesAsync(key, request);

			return result.Error switch
			{
				TalentTreeSaveError.None => NoContent(),
				TalentTreeSaveError.KeyRequired => BadRequest(new
				{
					message = "Talent tree key is required."
				}),
				TalentTreeSaveError.DataRequired => BadRequest(new
				{
					message = "Talent tree data is required."
				}),
				TalentTreeSaveError.KeyMismatch => BadRequest(new
				{
					message = "The route key does not match the request key."
				}),
				TalentTreeSaveError.NodesRequired => BadRequest(new
				{
					message = "Talent nodes are required."
				}),
				TalentTreeSaveError.InvalidNode => BadRequest(new
				{
					message = "Every talent node must contain a valid ID."
				}),
				TalentTreeSaveError.DuplicateNode => BadRequest(new
				{
					message = $"Duplicate talent node ID: {result.DuplicateNodeId}."
				}),
				_ => BadRequest()
			};
		}
	}
}
