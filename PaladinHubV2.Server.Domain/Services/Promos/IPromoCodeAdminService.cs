using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Promos
{
	public interface IPromoCodeAdminService
	{
		Task<List<PromoCode>> GetAllAsync(CancellationToken cancellationToken = default);
		PromoCode BuildCreateModel();
		IReadOnlyDictionary<string, string[]> NormalizeAndValidate(PromoCode model);
		Task<PromoCode?> CreateAsync(PromoCode model, CancellationToken cancellationToken = default);
		Task<bool> DeactivateAsync(string id);
	}
}
