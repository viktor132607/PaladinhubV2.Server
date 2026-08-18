using System.Threading.Tasks;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Promos
{
	public interface IPromoCodeService
	{
		Task<(bool ok, string msg, decimal? amount, string? currency, int? percent)> RedeemAsync(User user, string rawCode, string fallbackCurrency);
		Task<PromoCode> CreateAsync(PromoCode code);
		Task<bool> DeactivateAsync(string id);
	}
}
