using System.Security.Claims;
using PaladinHubV2.Common.Requests.Auth;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public enum AuthOperationStatus
	{
		Ok,
		Accepted,
		BadRequest,
		Unauthorized,
		Forbidden,
		Conflict,
		Locked,
		InternalError
	}

	public sealed record AuthOperationResult(
		AuthOperationStatus Status,
		object Payload);

	public interface IAuthService
	{
		Task<AuthOperationResult> GetCurrentUserAsync(ClaimsPrincipal principal);
		Task<AuthOperationResult> RegisterAsync(RegisterRequest request);
		Task<AuthOperationResult> LoginAsync(LoginRequest request);
		Task<AuthOperationResult> LoginWithTwoFactorAsync(TwoFactorLoginRequest request);
		Task<AuthOperationResult> LoginWithRecoveryCodeAsync(RecoveryCodeLoginRequest request);
		Task<AuthOperationResult> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request);
		Task<AuthOperationResult> LogoutAsync();
	}
}
