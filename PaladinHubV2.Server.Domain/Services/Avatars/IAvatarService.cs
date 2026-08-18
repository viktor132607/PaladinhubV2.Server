using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Common;

namespace PaladinHubV2.Server.Domain.Services.Avatars
{
	public interface IAvatarService
	{
		Task<OperationResult> SetDefaultAvatar(User user, string file);
		Task<OperationResult> UploadAvatar(User user, IFormFile file);
		Task<OperationResult> SetUploadedAvatar(User user, string path);
		Task<OperationResult> DeleteUpload(User user, string path);
	}

	public sealed class AvatarService : IAvatarService
	{
		private readonly IWebHostEnvironment _environment;
		private readonly UserManager<User> _userManager;

		public AvatarService(
			IWebHostEnvironment environment,
			UserManager<User> userManager)
		{
			_environment = environment;
			_userManager = userManager;
		}

		public async Task<OperationResult> SetDefaultAvatar(
			User user,
			string file)
		{
			if (string.IsNullOrWhiteSpace(file) ||
				Path.GetFileName(file) != file)
			{
				return OperationResult.Fail("Invalid avatar file.");
			}

			user.AvatarPath = $"/images/avatars/{file}";

			IdentityResult updateResult =
				await _userManager.UpdateAsync(user);

			return updateResult.Succeeded
				? OperationResult.Success(
					"Profile picture updated.",
					user.AvatarPath)
				: OperationResult.Fail(
					"Avatar could not be updated.");
		}

		public async Task<OperationResult> UploadAvatar(
			User user,
			IFormFile file)
		{
			if (file == null || file.Length == 0)
			{
				return OperationResult.Fail(
					"No file was provided.");
			}

			string extension = Path
				.GetExtension(file.FileName)
				.ToLowerInvariant();

			if (extension is not ".jpg"
				and not ".jpeg"
				and not ".png"
				and not ".webp")
			{
				return OperationResult.Fail(
					"Unsupported image format.");
			}

			string uploadsRoot = GetAvatarDirectory(user.Id);
			Directory.CreateDirectory(uploadsRoot);

			string fileName = $"{Guid.NewGuid():N}{extension}";
			string fullPath = Path.Combine(uploadsRoot, fileName);

			await using FileStream stream = File.Create(fullPath);
			await file.CopyToAsync(stream);

			string webPath =
				$"/uploads/avatars/{user.Id}/{fileName}";

			return OperationResult.Success(
				"Avatar uploaded.",
				webPath);
		}

		public async Task<OperationResult> SetUploadedAvatar(
			User user,
			string path)
		{
			if (!TryResolveOwnedUpload(
					user.Id,
					path,
					out string fullPath))
			{
				return OperationResult.Fail(
					"Invalid avatar path.");
			}

			if (!File.Exists(fullPath))
			{
				return OperationResult.Fail(
					"Avatar file was not found.");
			}

			user.AvatarPath = path;

			IdentityResult updateResult =
				await _userManager.UpdateAsync(user);

			return updateResult.Succeeded
				? OperationResult.Success(
					"Profile picture updated.",
					path)
				: OperationResult.Fail(
					"Avatar could not be updated.");
		}

		public async Task<OperationResult> DeleteUpload(
			User user,
			string path)
		{
			if (!TryResolveOwnedUpload(
					user.Id,
					path,
					out string fullPath))
			{
				return OperationResult.Fail(
					"Invalid avatar path.");
			}

			if (!File.Exists(fullPath))
			{
				return OperationResult.Fail(
					"Avatar file was not found.");
			}

			File.Delete(fullPath);

			if (string.Equals(
					user.AvatarPath,
					path,
					StringComparison.OrdinalIgnoreCase))
			{
				user.AvatarPath = null;

				IdentityResult updateResult =
					await _userManager.UpdateAsync(user);

				if (!updateResult.Succeeded)
				{
					return OperationResult.Fail(
						"Avatar record could not be updated.");
				}
			}

			return OperationResult.Success("Deleted.");
		}

		private string GetAvatarDirectory(string userId)
		{
			string webRoot = string.IsNullOrWhiteSpace(
					_environment.WebRootPath)
				? Path.Combine(
					_environment.ContentRootPath,
					"wwwroot")
				: _environment.WebRootPath;

			return Path.Combine(
				webRoot,
				"uploads",
				"avatars",
				userId);
		}

		private bool TryResolveOwnedUpload(
			string userId,
			string? webPath,
			out string fullPath)
		{
			fullPath = string.Empty;

			if (string.IsNullOrWhiteSpace(webPath))
			{
				return false;
			}

			string expectedPrefix =
				$"/uploads/avatars/{userId}/";

			if (!webPath.StartsWith(
					expectedPrefix,
					StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			try
			{
				string fileName = Path.GetFileName(webPath);
				if (string.IsNullOrWhiteSpace(fileName))
				{
					return false;
				}

				string avatarDirectory = Path.GetFullPath(
					GetAvatarDirectory(userId));

				string candidate = Path.GetFullPath(
					Path.Combine(avatarDirectory, fileName));

				if (!candidate.StartsWith(
						avatarDirectory + Path.DirectorySeparatorChar,
						StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}

				fullPath = candidate;
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
