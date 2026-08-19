using System.Security.Claims;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public enum AccountSecurityStatus
	{
		Ok,
		BadRequest,
		Unauthorized,
		Conflict,
		InternalError
	}

	public sealed record AccountSecurityResult(
		AccountSecurityStatus Status,
		object Payload);

	public interface IAccountSecurityApplicationService
	{
		Task<AccountSecurityResult> GetOverviewAsync(ClaimsPrincipal principal, bool requireTwoFactor, string device);
		Task<AccountSecurityResult> GetEnableTwoFactorAsync(ClaimsPrincipal principal, bool reset);
		Task<AccountSecurityResult> EnableTwoFactorAsync(ClaimsPrincipal principal, string? code);
		Task<AccountSecurityResult> DisableTwoFactorAsync(ClaimsPrincipal principal);
		Task<AccountSecurityResult> GenerateRecoveryCodesAsync(ClaimsPrincipal principal);
		Task<AccountSecurityResult> ValidateRequireTwoFactorAsync(ClaimsPrincipal principal, bool on);
		Task<AccountSecurityResult> LogoutAllDevicesAsync(ClaimsPrincipal principal);
	}
}
