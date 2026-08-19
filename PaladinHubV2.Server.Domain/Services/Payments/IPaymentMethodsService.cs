using System.Security.Claims;
using PaladinHubV2.Server.Data.Entities;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public sealed record PaymentMethodsPageData(
		string Region,
		string RegionCode,
		string Currency,
		decimal Balance,
		IReadOnlyList<DbPaymentMethod> Methods);

	public interface IPaymentMethodsService
	{
		Task<PaymentMethodsPageData?> GetPageAsync(
			ClaimsPrincipal principal);

		string? GetStripePublishableKey();
		Task<string> EnsureStripeCustomer(User me);
		Task<List<DbPaymentMethod>> GetMethods(User me);
		Task AddStripePaymentMethod(User me, string paymentMethodId);
		Task<bool> RemovePaymentMethod(User me, string id);
		Task<bool> SetDefaultPaymentMethod(User me, string id);
	}
}
