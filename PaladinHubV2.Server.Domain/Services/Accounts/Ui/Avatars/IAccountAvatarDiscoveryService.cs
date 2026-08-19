namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountAvatarDiscoveryService
	{
		IEnumerable<string> GetUserUploadedAvatars(string userId);
		void RegisterUserUploadedAvatar(string userId, string webPath);
		void UnregisterUserUploadedAvatar(string userId, string webPath);
	}
}
