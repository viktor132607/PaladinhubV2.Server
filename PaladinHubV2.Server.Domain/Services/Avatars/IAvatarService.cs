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

	public class AvatarService : IAvatarService
	{
		private readonly IWebHostEnvironment _env;
		private readonly UserManager<User> _um;

		public AvatarService(IWebHostEnvironment env, UserManager<User> um)
		{
			_env = env;
			_um = um;
		}

		public async Task<OperationResult> SetDefaultAvatar(User user, string file)
		{
			var nameOnly = System.IO.Path.GetFileName(file);
			var allowed = Enumerable.Range(1, 39).Select(i => $"default{i:00}.png").ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (!allowed.Contains(nameOnly)) return OperationResult.Fail("Invalid avatar.");
			var physical = System.IO.Path.Combine(_env.WebRootPath, "images", "avatars", nameOnly);
			if (!System.IO.File.Exists(physical)) return OperationResult.Fail("Avatar not found on server.");
			user.AvatarPath = $"/images/avatars/{nameOnly}";
			await _um.UpdateAsync(user);
			return OperationResult.Success("Profile picture updated.", user.AvatarPath);
		}

		public async Task<OperationResult> UploadAvatar(User user, IFormFile file)
		{
			if (file == null || file.Length == 0) return OperationResult.Fail("Please choose an image file.");
			var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
			if (!allowedTypes.Contains(file.ContentType)) return OperationResult.Fail("Only JPEG, PNG or WEBP images are allowed.");
			const long MAX = 2 * 1024 * 1024;
			if (file.Length > MAX) return OperationResult.Fail("Image must be up to 2 MB.");
			var userDir = System.IO.Path.Combine(_env.WebRootPath, "images", "avatars", "users", user.Id);
			System.IO.Directory.CreateDirectory(userDir);
			var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
			if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
			{
				ext = file.ContentType switch
				{
					"image/jpeg" => ".jpg",
					"image/png" => ".png",
					"image/webp" => ".webp",
					_ => ".jpg"
				};
			}
			var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
			var fileName = $"avatar_{stamp}{ext}";
			var fullPath = System.IO.Path.Combine(userDir, fileName);
			await using (var stream = System.IO.File.Create(fullPath)) { await file.CopyToAsync(stream); }
			user.AvatarPath = $"/images/avatars/users/{user.Id}/{fileName}";
			await _um.UpdateAsync(user);
			return OperationResult.Success("Profile picture updated.", user.AvatarPath);
		}

		public async Task<OperationResult> SetUploadedAvatar(User user, string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return OperationResult.Fail("Invalid image.");
			var expectedPrefix = $"/images/avatars/users/{user.Id}/";
			if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) return OperationResult.Fail("Not your image.");
			var phys = System.IO.Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar));
			if (!System.IO.File.Exists(phys)) return OperationResult.Fail("Image not found.");
			user.AvatarPath = path;
			await _um.UpdateAsync(user);
			return OperationResult.Success("Profile picture updated.", user.AvatarPath);
		}

		public Task<OperationResult> DeleteUpload(User user, string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(OperationResult.Fail("Invalid path."));
			var expectedPrefix = $"/images/avatars/users/{user.Id}/";
			if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) return Task.FromResult(OperationResult.Fail("Not your image."));
			var phys = System.IO.Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar));
			if (System.IO.File.Exists(phys)) System.IO.File.Delete(phys);
			return Task.FromResult(OperationResult.Success("Deleted."));
		}
	}
}
