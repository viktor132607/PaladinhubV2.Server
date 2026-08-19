using System.Security.Claims;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public partial class PaymentMethodsService
	{
		public Task<PaymentMethodsPageData?> GetPageAsync(ClaimsPrincipal principal) =>
			_page.GetPageAsync(principal);
	}
}
