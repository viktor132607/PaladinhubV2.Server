using PaladinHub.Models;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products;

public interface IProductCatalogQueryService
{
    Task<ICollection<ProductViewModel>> GetAllAsync();
    Task<MyCartViewModel> GetMyProductsAsync(User user);
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<EditProductViewModel?> GetForEditAsync(string id, CancellationToken ct = default);
    Task<ProductDetailsViewModel?> GetDetailsAsync(string id, CancellationToken ct);
    Task<ProductDetailsViewModel?> GetDetailsAsync(
        string id,
        string? currentUserId,
        bool isAdmin,
        CancellationToken ct);
}

public interface IProductSearchService
{
    Task<PagedResult<ProductListItem>> QueryAsync(
        ProductQueryOptions options,
        CancellationToken ct = default);
}

public interface IProductMutationService
{
    Task<CreateProductViewModel> CreateAsync(CreateProductViewModel model);
    Task<bool> DeleteAsync(string id);
    Task<bool> UpdateAsync(EditProductViewModel model, CancellationToken ct = default);
    Task<bool> AddImageAsync(
        string productId,
        string url,
        int? sortOrder,
        CancellationToken ct);
    Task<bool> RemoveImageAsync(int imageId, CancellationToken ct);
}

public interface IProductReviewService
{
    Task<bool> AddReviewAsync(
        AddReviewInput input,
        string userId,
        CancellationToken ct);
    Task<bool> DeleteReviewAsync(
        int reviewId,
        string userId,
        bool isAdmin,
        CancellationToken ct);
}

internal readonly record struct ProductPriceBand(decimal? Min, decimal? Max);
