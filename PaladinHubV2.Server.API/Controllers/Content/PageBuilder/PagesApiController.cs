using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/pages")]
	public sealed class PagesApiController : ControllerBase
	{
		private readonly IPageService _pages;

		public PagesApiController(IPageService pages)
		{
			_pages = pages;
		}

		[HttpPut("{id:int}/layout")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> PutLayout(
			[FromRoute] int id,
			[FromBody] PutPageLayoutRequest? request)
		{
			if (id <= 0)
			{
				return BadRequest(new
				{
					message = "Invalid page ID."
				});
			}

			if (request == null ||
				string.IsNullOrWhiteSpace(request.JsonLayout))
			{
				return BadRequest(new
				{
					message = "JsonLayout is required."
				});
			}

			if (!TryDecodeRowVersion(
					request.RowVersionBase64,
					out byte[] rowVersion))
			{
				return BadRequest(new
				{
					message = "RowVersionBase64 is invalid or empty."
				});
			}

			var existingPage = await _pages.GetByIdAsync(id);

			if (existingPage == null)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			try
			{
				string updatedBy =
					User.Identity?.Name ?? "admin";

				var (updated, newRowVersion) =
					await _pages.UpdateLayoutSafeAsync(
						id,
						request.JsonLayout.Trim(),
						rowVersion,
						updatedBy);

				if (!updated || newRowVersion == null)
				{
					return Conflict(new
					{
						message =
							"The page was modified by someone else. Refresh and try again."
					});
				}

				return Ok(new
				{
					id,
					rowVersionBase64 =
						Convert.ToBase64String(newRowVersion)
				});
			}
			catch (JsonLayoutValidationException exception)
			{
				return BadRequest(new
				{
					message = "Layout validation failed.",
					errors = exception.Errors
				});
			}
		}

		[HttpGet("{id:int}/head")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> GetHead(
			[FromRoute] int id)
		{
			if (id <= 0)
			{
				return BadRequest(new
				{
					message = "Invalid page ID."
				});
			}

			var page = await _pages.GetByIdAsync(id);

			if (page == null)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			return Ok(new
			{
				id = page.Id,
				rowVersionBase64 =
					Convert.ToBase64String(
						page.RowVersion ?? Array.Empty<byte>()),
				updatedAt = page.UpdatedAt
			});
		}

		private static bool TryDecodeRowVersion(
			string? value,
			out byte[] rowVersion)
		{
			rowVersion = Array.Empty<byte>();

			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			try
			{
				rowVersion = Convert.FromBase64String(value.Trim());
				return rowVersion.Length > 0;
			}
			catch (FormatException)
			{
				return false;
			}
		}
	}
}
