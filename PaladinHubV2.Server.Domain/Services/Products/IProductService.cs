using PaladinHub.Models;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public interface IProductService
	{
		Task<ICollection<ProductViewModel>> GetAll();

		Task<CreateProductViewModel> Create(CreateProductViewModel model);

		Task<bool> Delete(string id);

		Task<List<string>> GetAllCategoriesAsync(CancellationToken ct = default);
		Task<List<string>> GetCategories();

		Task<EditProductViewModel?> GetForEditAsync(string id, CancellationToken ct = default);
		Task<bool> UpdateAsync(EditProductViewModel model, CancellationToken ct = default);

		Task<MyCartViewModel> GetMyProducts(Data.Entities.User user);

		// ✅ ВАЖНО: връща ProductListItem (не ProductViewModel)
		Task<PagedResult<ProductListItem>> QueryAsync(ProductQueryOptions options, CancellationToken ct = default);

		Task<ProductDetailsViewModel?> GetDetailsAsync(string id, CancellationToken ct);
		Task<ProductDetailsViewModel?> GetDetailsAsync(string id, string? currentUserId, bool isAdmin, CancellationToken ct);

		Task<bool> AddReviewAsync(AddReviewInput input, string userId, CancellationToken ct);
		Task<bool> DeleteReviewAsync(int reviewId, string userId, bool isAdmin, CancellationToken ct);

		Task<bool> AddImageAsync(string productId, string url, int? sortOrder, CancellationToken ct);
		Task<bool> RemoveImageAsync(int imageId, CancellationToken ct);
	}
}
