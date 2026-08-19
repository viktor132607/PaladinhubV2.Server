namespace PaladinHubV2.Common.Responses.Auth
{
	public sealed record AuthUserResponse(
		string Id,
		string Username,
		string Email,
		string FullName,
		string? AvatarPath,
		string[] Roles);

	public sealed record AuthSessionResponse(
		bool IsAuthenticated,
		AuthUserResponse? User)
	{
		public static AuthSessionResponse Anonymous { get; } = new(false, null);
	}

	public sealed record AuthErrorResponse(
		string Message,
		string[]? Errors = null);
}
