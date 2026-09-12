namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public enum AccountAvatarFailure
	{
		None = 0,
		NoFile = 1,
		UnsupportedFormat = 2,
		InvalidPath = 3,
		NotFound = 4,
		InvalidDefaultAvatar = 5
	}

	public sealed record AccountAvatarResult(
		bool Ok,
		AccountAvatarFailure Failure,
		string? Message,
		string? Path)
	{
		public static AccountAvatarResult Success(string? path = null)
		{
			return new AccountAvatarResult(
				true,
				AccountAvatarFailure.None,
				null,
				path);
		}

		public static AccountAvatarResult Fail(
			AccountAvatarFailure failure,
			string message)
		{
			return new AccountAvatarResult(
				false,
				failure,
				message,
				null);
		}
	}
}
