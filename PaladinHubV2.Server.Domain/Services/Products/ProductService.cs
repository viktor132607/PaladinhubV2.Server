using PaladinHub.Models;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products;

public sealed class ProductService : IProductService
{
    private readonly IProductCatalogQueryService _catalog;
    private readonly IProductSearchService _search;
    private readonly IProductMutationService _mutations;
    private readonly IProductReviewService _reviews;

    internal ProductService(AppDbContext db)
        : this(
            new ProductCatalogQueryService(db),
            new ProductSearchService(db),
            new ProductMutationService(db),
            new ProductReviewService(db))
    {
    }

    public ProductService(
        IProductCatalogQueryService catalog,
        IProductSearchService search,
        IProductMutationService mutations,
        IProductReviewService reviews)
    {
        _catalog = catalog;
        _search = search;
        _mutations = mutations;
        _reviews = reviews;
    }

    public Task<ICollection<ProductViewModel>> GetAll() =>
        _catalog.GetAllAsync();

    public Task<CreateProductViewModel> Create(CreateProductViewModel model) =>
        _mutations.CreateAsync(model);

    public Task<MyCartViewModel> GetMyProducts(User user) =>
        _catalog.GetMyProductsAsync(user);

    public Task<bool> Delete(string id) =>
        _mutations.DeleteAsync(id);

    public Task<List<string>> GetAllCategoriesAsync(
        CancellationToken ct = default) =>
        _catalog.GetCategoriesAsync(ct);

    public Task<List<string>> GetCategories() =>
        _catalog.GetCategoriesAsync();

    public Task<PagedResult<ProductListItem>> QueryAsync(
        ProductQueryOptions options,
        CancellationToken ct = default) =>
        _search.QueryAsync(options, ct);

    public Task<EditProductViewModel?> GetForEditAsync(
        string id,
        CancellationToken ct = default) =>
        _catalog.GetForEditAsync(id, ct);

    public Task<bool> UpdateAsync(
        EditProductViewModel model,
        CancellationToken ct = default) =>
        _mutations.UpdateAsync(model, ct);

    public Task<ProductDetailsViewModel?> GetDetailsAsync(
        string id,
        CancellationToken ct) =>
        _catalog.GetDetailsAsync(id, ct);

    public Task<ProductDetailsViewModel?> GetDetailsAsync(
        string id,
        string? currentUserId,
        bool isAdmin,
        CancellationToken ct) =>
        _catalog.GetDetailsAsync(id, currentUserId, isAdmin, ct);

    public Task<bool> AddReviewAsync(
        AddReviewInput input,
        string userId,
        CancellationToken ct) =>
        _reviews.AddReviewAsync(input, userId, ct);

    public Task<bool> DeleteReviewAsync(
        int reviewId,
        string userId,
        bool isAdmin,
        CancellationToken ct) =>
        _reviews.DeleteReviewAsync(reviewId, userId, isAdmin, ct);

    public Task<bool> AddImageAsync(
        string productId,
        string url,
        int? sortOrder,
        CancellationToken ct) =>
        _mutations.AddImageAsync(productId, url, sortOrder, ct);

    public Task<bool> RemoveImageAsync(
        int imageId,
        CancellationToken ct) =>
        _mutations.RemoveImageAsync(imageId, ct);
}
