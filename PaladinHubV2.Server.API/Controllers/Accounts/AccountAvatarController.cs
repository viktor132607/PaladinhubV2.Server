using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Avatars;
using PaladinHubV2.Server.Domain.Services.Common;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountAvatarController : ControllerBase
	{
		private readonly IAccountUiService _ui;
		private readonly IAvatarService _avatars;

		public AccountAvatarController(
			IAccountUiService ui,
			IAvatarService avatars)
		{
			_ui = ui;
			_avatars = avatars;
		}

		[HttpPost("UploadAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UploadAvatar(
			[FromForm] IFormFile file)
		{
			User? me = await _ui.GetMe(User);
			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			OperationResult result =
				await _avatars.UploadAvatar(me, file);

			if (!result.Ok)
			{
				var error = new
				{
					ok = false,
					message = result.Message
				};

				return result.Message == "Unsupported image format."
					? StatusCode(
						StatusCodes.Status415UnsupportedMediaType,
						error)
					: BadRequest(error);
			}

			return Ok(new
			{
				ok = true,
				path = result.Path
			});
		}

		[HttpPost("SetUploadedAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetUploadedAvatar(
			[FromForm] string path)
		{
			User? me = await _ui.GetMe(User);
			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			OperationResult result =
				await _avatars.SetUploadedAvatar(me, path);

			if (!result.Ok)
			{
				var error = new
				{
					ok = false,
					message = result.Message
				};

				return result.Message == "Avatar file was not found."
					? NotFound(error)
					: BadRequest(error);
			}

			return Ok(new
			{
				ok = true,
				path = result.Path
			});
		}

		[HttpPost("DeleteUpload")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteUpload(
			[FromForm] string path)
		{
			return DeleteUploadCore(path);
		}

		[HttpDelete("DeleteUpload")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteUploadByQuery(
			[FromQuery] string path)
		{
			return DeleteUploadCore(path);
		}

		[HttpPost("SetDefaultAvatar")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetDefaultAvatar(
			[FromForm] string file)
		{
			User? me = await _ui.GetMe(User);
			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			OperationResult result =
				await _avatars.SetDefaultAvatar(me, file);

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
			string path)
		{
			User? me = await _ui.GetMe(User);
			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			OperationResult result =
				await _avatars.DeleteUpload(me, path);

			if (!result.Ok)
			{
				var error = new
				{
					ok = false,
					message = result.Message
				};

				return result.Message == "Avatar file was not found."
					? NotFound(error)
					: BadRequest(error);
			}

			return Ok(new { ok = true });
		}
	}
}
