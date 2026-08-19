using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Auth
{
	public sealed class LoginRequest
	{
		[Required]
		public string Identifier { get; init; } = string.Empty;

		[Required]
		public string Password { get; init; } = string.Empty;

		public bool RememberMe { get; init; }
	}

	public sealed class RegisterRequest
	{
		[Required]
		[StringLength(100, MinimumLength = 2)]
		public string Name { get; init; } = string.Empty;

		[Required]
		[StringLength(32, MinimumLength = 3)]
		[RegularExpression(@"^[a-zA-Z0-9._-]+$")]
		public string Username { get; init; } = string.Empty;

		[Required]
		[EmailAddress]
		public string Email { get; init; } = string.Empty;

		[Required]
		[StringLength(40, MinimumLength = 8)]
		public string Password { get; init; } = string.Empty;

		[Required]
		[Compare(nameof(Password))]
		public string ConfirmPassword { get; init; } = string.Empty;
	}

	public sealed class TwoFactorLoginRequest
	{
		[Required]
		public string Code { get; init; } = string.Empty;

		public bool RememberMe { get; init; }
		public bool RememberMachine { get; init; }
	}

	public sealed class RecoveryCodeLoginRequest
	{
		[Required]
		public string RecoveryCode { get; init; } = string.Empty;
	}

	public sealed class ChangePasswordRequest
	{
		[Required]
		public string OldPassword { get; init; } = string.Empty;

		[Required]
		[StringLength(40, MinimumLength = 8)]
		public string NewPassword { get; init; } = string.Empty;

		[Required]
		[Compare(nameof(NewPassword))]
		public string ConfirmNewPassword { get; init; } = string.Empty;
	}
}
