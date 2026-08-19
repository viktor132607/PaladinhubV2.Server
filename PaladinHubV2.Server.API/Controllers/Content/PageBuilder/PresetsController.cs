using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Domain.Services.Presets;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Route("api/presets")]
	[Authorize(Roles = "Admin")]
	public sealed class PresetsController : ControllerBase
	{
		private readonly IDataPresetService _presets;

		public PresetsController(IDataPresetService presets)
		{
			_presets = presets;
		}

		[HttpGet]
		public async Task<IActionResult> List(
			[FromQuery] string? entity,
			[FromQuery] string? section,
			CancellationToken cancellationToken)
		{
			var rows = await _presets.ListAsync(
				entity,
				section,
				cancellationToken);

			return Ok(rows.Select(preset => new
			{
				preset.Id,
				preset.Name,
				preset.Entity,
				preset.Section,
				preset.UpdatedAt
			}));
		}

		[HttpGet("{id:int}")]
		public async Task<IActionResult> Get(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			var preset = await _presets.GetAsync(
				id,
				cancellationToken);

			return preset == null
				? NotFound()
				: Ok(preset);
		}

		[HttpPost]
		public async Task<IActionResult> Create(
			[FromBody] CreateDataPresetRequest request,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(request.Name) ||
				string.IsNullOrWhiteSpace(request.Entity))
			{
				return BadRequest(new
				{
					message = "Name and Entity are required."
				});
			}

			var created = await _presets.CreateAsync(
				request.Name,
				request.Entity,
				request.JsonQuery ?? "{}",
				request.Section,
				cancellationToken);

			return CreatedAtAction(
				nameof(Get),
				new { id = created.Id },
				created);
		}

		[HttpPut("{id:int}")]
		public async Task<IActionResult> Update(
			[FromRoute] int id,
			[FromBody] UpdateDataPresetRequest request,
			CancellationToken cancellationToken)
		{
			var updated = await _presets.UpdateAsync(
				id,
				request.Name,
				request.JsonQuery,
				request.Section,
				cancellationToken);

			return updated == null
				? NotFound()
				: Ok(updated);
		}

		[HttpDelete("{id:int}")]
		public async Task<IActionResult> Delete(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			bool deleted = await _presets.DeleteAsync(
				id,
				cancellationToken);

			return deleted
				? NoContent()
				: NotFound();
		}

		[HttpGet("{id:int}/preview")]
		public async Task<IActionResult> Preview(
			[FromRoute] int id,
			[FromQuery] int? take,
			CancellationToken cancellationToken)
		{
			try
			{
				var rows = await _presets.ResolveAsync(
					id,
					take,
					cancellationToken);

				return Ok(new
				{
					count = rows.Count,
					rows
				});
			}
			catch (KeyNotFoundException)
			{
				return NotFound();
			}
		}
	}
}
