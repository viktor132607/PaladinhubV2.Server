using System.Security.Claims;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Wallet;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public sealed class PaymentMethodsPageService : IPaymentMethodsPageService
	{
		private readonly IAccountIdentityService _identity;
		private readonly IAccountRegionService _region;
		private readonly IWalletService _wallet;
		private readonly IPaymentMethodsStore _store;

		public PaymentMethodsPageService(
			IAccountIdentityService identity,
			IAccountRegionService region,
			IWalletService wallet,
			IPaymentMethodsStore store)
		{
			_identity = identity;
			_region = region;
			_wallet = wallet;
			_store = store;
		}

		public async Task<PaymentMethodsPageData?> GetPageAsync(
			ClaimsPrincipal principal)
		{
			User? user = await _identity.GetMe(principal);
			if (user == null)
			{
				return null;
			}

			string regionCode = _region.ReadRegionCookie() ?? "EU";
			string currency = _region.GetCurrencyForRegion(regionCode);
			decimal balance = await _wallet.GetBalanceAsync(user.Id);
			List<DbPaymentMethod> methods = await _store.GetMethods(user);

			return new PaymentMethodsPageData(
				_region.RegionDisplay(regionCode),
				regionCode,
				currency,
				balance,
				methods);
		}
	}
}
