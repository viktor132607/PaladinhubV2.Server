using Microsoft.AspNetCore.Hosting;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountAvatarDiscoveryService : IAccountAvatarDiscoveryService
	{
		private readonly IWebHostEnvironment _environment;

		public AccountAvatarDiscoveryService(IWebHostEnvironment environment)
		{
			_environment = environment;
		}

		public IEnumerable<string> GetUserUploadedAvatars(string userId)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Enumerable.Empty<string>();
			}

			string webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
				? Path.Combine(_environment.ContentRootPath, "wwwroot")
				: _environment.WebRootPath;

			string root = Path.Combine(webRoot, "uploads", "avatars", userId);

			if (!Directory.Exists(root))
			{
				return Enumerable.Empty<string>();
			}

			return Directory
				.EnumerateFiles(root)
				.OrderByDescending(File.GetCreationTimeUtc)
				.Select(path =>
					$"/uploads/avatars/{userId}/" + Path.GetFileName(path));
		}

		public void RegisterUserUploadedAvatar(string userId, string webPath)
		{
			// Uploads are discovered directly from the filesystem.
		}

		public void UnregisterUserUploadedAvatar(string userId, string webPath)
		{
			// Uploads are discovered directly from the filesystem.
		}
	}
}
