using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public interface IMerchandiseService
	{
		Task<MerchandisePageViewModel> GetPageAsync(
			ProductQueryOptions options,
			CancellationToken cancellationToken = default);
	}
}
