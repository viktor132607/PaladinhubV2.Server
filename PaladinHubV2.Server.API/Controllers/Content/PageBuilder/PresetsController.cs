using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Presets;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Route("api/presets")]
	[Authorize(Roles = "Admin")]
	public sealed class PresetsController : ControllerBase
	{
		private readonly IDataPresetService _presets;
		public PresetsController(IDataPresetService presets) { _presets = presets; }

		[HttpGet]
		public async Task<IActionResult> List([FromQuery] string? entity, [FromQuery] string? section, CancellationToken ct)
		{
			var rows = await _presets.ListAsync(entity, section, ct);
			return Ok(rows.Select(p => new {
				p.Id,
				p.Name,
				p.Entity,
				p.Section,
				p.UpdatedAt
			}));
		}

		[HttpGet("{id:int}")]
		public async Task<IActionResult> Get(int id, CancellationToken ct)
		{
			var row = await _presets.GetAsync(id, ct);
			return row == null ? NotFound() : Ok(row);
		}

		public sealed record CreateReq(string Name, string Entity, string JsonQuery, string? Section);
		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CreateReq req, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Entity))
				return BadRequest(new { message = "Name and Entity are required." });

			var created = await _presets.CreateAsync(req.Name, req.Entity, req.JsonQuery ?? "{}", req.Section, ct);
			return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
		}

		public sealed record UpdateReq(string? Name, string? JsonQuery, string? Section);
		[HttpPut("{id:int}")]
		public async Task<IActionResult> Update(int id, [FromBody] UpdateReq req, CancellationToken ct)
		{
			var updated = await _presets.UpdateAsync(id, req.Name, req.JsonQuery, req.Section, ct);
			return updated == null ? NotFound() : Ok(updated);
		}

		[HttpDelete("{id:int}")]
		public async Task<IActionResult> Delete(int id, CancellationToken ct)
		{
			var ok = await _presets.DeleteAsync(id, ct);
			return ok ? NoContent() : NotFound();
		}

		[HttpGet("{id:int}/preview")]
		public async Task<IActionResult> Preview(int id, [FromQuery] int? take, CancellationToken ct)
		{
			try
			{
				var rows = await _presets.ResolveAsync(id, take, ct);
				return Ok(new { count = rows.Count, rows });
			}
			catch (KeyNotFoundException)
			{
				return NotFound();
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = "Failed to resolve preset", error = ex.Message });
			}
		}
	}
}
