namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed partial class AccountUiService
	{
		public IEnumerable<string> GetUserUploadedAvatars(string userId) =>
			_avatars.GetUserUploadedAvatars(userId);

		public void RegisterUserUploadedAvatar(string userId, string webPath) =>
			_avatars.RegisterUserUploadedAvatar(userId, webPath);

		public void UnregisterUserUploadedAvatar(string userId, string webPath) =>
			_avatars.UnregisterUserUploadedAvatar(userId, webPath);
	}
}
