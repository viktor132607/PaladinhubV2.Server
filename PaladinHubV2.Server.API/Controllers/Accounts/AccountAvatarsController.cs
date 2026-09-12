using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountAvatarsController : ControllerBase
	{
		private readonly IAccountUiService _ui;

		public AccountAvatarsController(IAccountUiService ui)
		{
			_ui = ui;
		}

		[HttpPost("UploadAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UploadAvatar(
			[FromForm] IFormFile file,
			CancellationToken cancellationToken)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			AccountAvatarResult result =
				await _ui.UploadAvatarAsync(
					me,
					file,
					cancellationToken);

			if (result.Ok)
			{
				return Ok(new
				{
					ok = true,
					path = result.Path
				});
			}

			if (result.Failure ==
				AccountAvatarFailure.UnsupportedFormat)
			{
				return StatusCode(
					StatusCodes.Status415UnsupportedMediaType,
					new
					{
						ok = false,
						message = result.Message
					});
			}

			return BadRequest(new
			{
				ok = false,
				message = result.Message
			});
		}

		[HttpPost("SetUploadedAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetUploadedAvatar(
			[FromForm] string path,
			CancellationToken cancellationToken)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			AccountAvatarResult result =
				await _ui.SetUploadedAvatarAsync(
					me,
					path,
					cancellationToken);

			if (result.Ok)
			{
				return Ok(new
				{
					ok = true,
					path = result.Path
				});
			}

			return AvatarError(result);
		}

		[HttpPost("DeleteUpload")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteUpload(
			[FromForm] string path,
			CancellationToken cancellationToken)
		{
			return await DeleteUploadCore(
				path,
				cancellationToken);
		}

		[HttpDelete("DeleteUpload")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteUploadByQuery(
			[FromQuery] string path,
			CancellationToken cancellationToken)
		{
			return await DeleteUploadCore(
				path,
				cancellationToken);
		}

		[HttpPost("SetDefaultAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetDefaultAvatar(
			[FromForm] string file,
			CancellationToken cancellationToken)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			AccountAvatarResult result =
				await _ui.SetDefaultAvatarAsync(
					me,
					file,
					cancellationToken);

			if (!result.Ok)
			{
				return BadRequest(new
				{
					ok = false,
					message = result.Message
				});
			}

			return Ok(new
			{
				ok = true,
				path = result.Path
			});
		}

		private async Task<IActionResult> DeleteUploadCore(
			string path,
			CancellationToken cancellationToken)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			AccountAvatarResult result =
				await _ui.DeleteUploadAsync(
					me,
					path,
					cancellationToken);

			if (result.Ok)
			{
				return Ok(new { ok = true });
			}

			return AvatarError(result);
		}

		private IActionResult AvatarError(
			AccountAvatarResult result)
		{
			if (result.Failure == AccountAvatarFailure.NotFound)
			{
				return NotFound(new
				{
					ok = false,
					message = result.Message
				});
			}

			return BadRequest(new
			{
				ok = false,
				message = result.Message
			});
		}
	}
}
