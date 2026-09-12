using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public interface IProductAdminFormService
	{
		Task<CreateProductViewModel> BuildCreateModelAsync();
		Task<EditProductViewModel?> BuildEditModelAsync(
			string id,
			CancellationToken cancellationToken = default);

		void ApplyNewCategory(CreateProductViewModel model);
		void ApplyNewCategory(EditProductViewModel model);
	}
}
