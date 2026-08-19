using System.Security.Claims;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public interface IPaymentMethodsPageService
	{
		Task<PaymentMethodsPageData?> GetPageAsync(ClaimsPrincipal principal);
	}
}
