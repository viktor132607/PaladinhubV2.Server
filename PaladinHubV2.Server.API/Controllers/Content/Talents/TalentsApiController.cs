using System;
using System.Linq;
using System.Threading.Tasks;
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
			var normalizedKey = key?.Trim();

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

			var requestKey = request.Key?.Trim();

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

			if (request.Nodes == null)
			{
				return BadRequest(new
				{
					message = "Talent nodes are required."
				});
			}

			if (request.Nodes.Any(node =>
					node == null ||
					string.IsNullOrWhiteSpace(node.Id)))
			{
				return BadRequest(new
				{
					message =
							"Every talent node must contain a valid ID."
				});
			}

			var normalizedNodes = request.Nodes
				.Select(node => new NodeState(
					node.Id.Trim(),
					node.Active))
				.ToList();

			var duplicateNodeId = normalizedNodes
				.GroupBy(
					node => node.Id,
					StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault(group => group.Count() > 1)
				?.Key;

			if (duplicateNodeId != null)
			{
				return BadRequest(new
				{
					message =
							$"Duplicate talent node ID: {duplicateNodeId}."
				});
			}

			await _trees.SaveActiveStatesAsync(
				normalizedKey,
				normalizedNodes);

			return NoContent();
		}
	}
}
