using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public interface IProductAdminService
	{
		Task<CreateProductViewModel> BuildCreateModelAsync(
			CancellationToken cancellationToken = default);

		void Normalize(CreateProductViewModel model);
		void Normalize(EditProductViewModel model);

		Task<CreateProductViewModel?> CreateAsync(
			CreateProductViewModel model,
			CancellationToken cancellationToken = default);

		Task<EditProductViewModel?> GetForEditAsync(
			string id,
			CancellationToken cancellationToken = default);

		Task<bool> UpdateAsync(
			EditProductViewModel model,
			CancellationToken cancellationToken = default);

		Task<bool> DeleteAsync(string id);
	}
}
