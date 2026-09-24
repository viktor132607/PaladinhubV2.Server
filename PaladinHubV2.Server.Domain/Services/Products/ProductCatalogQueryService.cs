using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products;

public sealed class ProductCatalogQueryService : IProductCatalogQueryService
{
    private readonly AppDbContext _db;

    public ProductCatalogQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ICollection<ProductViewModel>> GetAllAsync()
    {
        return await _db.Products
            .AsNoTracking()
            .Select(product => new ProductViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                ImageUrl =
                    _db.ProductImages
                        .Where(image =>
                            image.ProductId == product.Id &&
                            image.Id == product.ThumbnailImageId)
                        .Select(image => image.Url)
                        .FirstOrDefault()
                    ?? _db.ProductImages
                        .Where(image => image.ProductId == product.Id)
                        .OrderBy(image => image.SortOrder)
                        .ThenBy(image => image.Id)
                        .Select(image => image.Url)
                        .FirstOrDefault(),
                Category = product.Category,
                Description = product.Description
            })
            .ToListAsync();
    }

    public async Task<MyCartViewModel> GetMyProductsAsync(User user)
    {
        var model = new MyCartViewModel
        {
            MyProducts = [],
            TotalPrice = 0m
        };

        if (user is null)
        {
            return model;
        }

        Cart? cart = await _db.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == user.Id);

        if (cart is null)
        {
            return model;
        }

        List<CartProduct> cartProducts = await _db.CartProducts
            .Include(item => item.Product)
            .Where(item => item.CartId == cart.Id)
            .ToListAsync();

        if (cartProducts.Count == 0)
        {
            return model;
        }

        List<string> productIds = cartProducts
            .Select(item => item.ProductId)
            .Distinct()
            .ToList();

        var thumbnails = await _db.Products
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                Url =
                    _db.ProductImages
                        .Where(image =>
                            image.ProductId == product.Id &&
                            image.Id == product.ThumbnailImageId)
                        .Select(image => image.Url)
                        .FirstOrDefault()
                    ?? _db.ProductImages
                        .Where(image => image.ProductId == product.Id)
                        .OrderBy(image => image.SortOrder)
                        .ThenBy(image => image.Id)
                        .Select(image => image.Url)
                        .FirstOrDefault()
            })
            .ToListAsync();

        Dictionary<string, string?> thumbnailMap =
            thumbnails.ToDictionary(item => item.Id, item => item.Url);

        foreach (CartProduct cartProduct in cartProducts)
        {
            if (!model.MyProducts.Any(item => item.Id == cartProduct.ProductId))
            {
                model.MyProducts.Add(new ProductViewModel
                {
                    Id = cartProduct.ProductId,
                    Name = cartProduct.Product?.Name ?? string.Empty,
                    Price = cartProduct.Product?.Price ?? 0m,
                    ImageUrl = thumbnailMap.GetValueOrDefault(cartProduct.ProductId),
                    Category = cartProduct.Product?.Category,
                    Description = cartProduct.Product?.Description,
                    Quantity = cartProduct.Quantity,
                    CartId = cart.Id,
                    Cart = null
                });
            }

            model.TotalPrice +=
                (cartProduct.Product?.Price ?? 0m) * cartProduct.Quantity;
        }

        return model;
    }

    public async Task<List<string>> GetCategoriesAsync(
        CancellationToken ct = default)
    {
        return await _db.Products
            .AsNoTracking()
            .Select(product => product.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(ct);
    }

    public async Task<EditProductViewModel?> GetForEditAsync(
        string id,
        CancellationToken ct = default)
    {
        Product? product = await _db.Products
            .AsNoTracking()
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (product is null)
        {
            return null;
        }

        var model = new EditProductViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            Category = product.Category,
            Description = product.Description,
            ThumbnailImageId = product.ThumbnailImageId,
            Images = product.Images
                .OrderBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Select(image => new ProductImageInputModel
                {
                    Id = image.Id,
                    Url = image.Url,
                    SortOrder = image.SortOrder,
                    AltText = image.AltText
                })
                .ToList()
        };

        if (model.ThumbnailImageId.HasValue)
        {
            List<ProductImage> ordered = product.Images
                .OrderBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .ToList();
            int index = ordered.FindIndex(
                image => image.Id == model.ThumbnailImageId.Value);
            model.ThumbnailIndex = index >= 0 ? index : null;
        }

        return model;
    }

    public async Task<ProductDetailsViewModel?> GetDetailsAsync(
        string id,
        CancellationToken ct)
    {
        Product? product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (product is null)
        {
            return null;
        }

        List<ProductDetailsViewModel.ImageItem> images =
            await LoadImagesAsync(id, ct);
        string? thumbnailUrl =
            await ResolveThumbnailUrlAsync(product, ct);

        return new ProductDetailsViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            ImageUrl = thumbnailUrl,
            Category = product.Category,
            Description = product.Description,
            Images = images
        };
    }

    public async Task<ProductDetailsViewModel?> GetDetailsAsync(
        string id,
        string? currentUserId,
        bool isAdmin,
        CancellationToken ct)
    {
        Product? product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, ct);

        if (product is null)
        {
            return null;
        }

        List<ProductDetailsViewModel.ImageItem> images =
            await LoadImagesAsync(id, ct);
        string? thumbnailUrl =
            await ResolveThumbnailUrlAsync(product, ct);

        var reviewRows = await (
            from review in _db.ProductReviews
            where review.ProductId == id
            join user in _db.Users
                on review.UserId equals user.Id into userGroup
            from user in userGroup.DefaultIfEmpty()
            orderby review.CreatedAt descending
            select new
            {
                review.Id,
                review.UserId,
                review.Rating,
                review.Content,
                review.CreatedAt,
                Display =
                    user != null
                        ? user.Email ?? user.UserName
                        : review.UserId
            }).ToListAsync(ct);

        double average = reviewRows.Count == 0
            ? 0
            : reviewRows.Average(item => item.Rating);

        List<SimilarVm> similar = await _db.Products
            .AsNoTracking()
            .Where(item =>
                item.Category == product.Category &&
                item.Id != product.Id)
            .OrderByDescending(item => item.Id)
            .Take(8)
            .Select(item => new SimilarVm
            {
                Id = item.Id,
                Name = item.Name,
                Price = item.Price,
                ImageUrl =
                    _db.ProductImages
                        .Where(image =>
                            image.ProductId == item.Id &&
                            image.Id == item.ThumbnailImageId)
                        .Select(image => image.Url)
                        .FirstOrDefault()
                    ?? _db.ProductImages
                        .Where(image => image.ProductId == item.Id)
                        .OrderBy(image => image.SortOrder)
                        .ThenBy(image => image.Id)
                        .Select(image => image.Url)
                        .FirstOrDefault()
            })
            .ToListAsync(ct);

        return new ProductDetailsViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            ImageUrl = thumbnailUrl,
            Category = product.Category,
            Description = product.Description,
            AverageRating = Math.Round(average, 1),
            ReviewsCount = reviewRows.Count,
            Reviews = reviewRows
                .Select(item => new ReviewVm
                {
                    Id = item.Id,
                    UserName = item.Display ?? item.UserId,
                    Rating = item.Rating,
                    Content = item.Content,
                    CreatedAt = item.CreatedAt,
                    CanDelete =
                        isAdmin ||
                        (currentUserId != null &&
                         item.UserId == currentUserId)
                })
                .ToList(),
            Similar = similar,
            Images = images
        };
    }

    private async Task<List<ProductDetailsViewModel.ImageItem>>
        LoadImagesAsync(
            string productId,
            CancellationToken ct)
    {
        return await _db.ProductImages
            .Where(image => image.ProductId == productId)
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .Select(image => new ProductDetailsViewModel.ImageItem
            {
                Id = image.Id,
                Url = image.Url
            })
            .ToListAsync(ct);
    }

    private async Task<string?> ResolveThumbnailUrlAsync(
        Product product,
        CancellationToken ct)
    {
        return await _db.ProductImages
                .Where(image =>
                    image.ProductId == product.Id &&
                    image.Id == product.ThumbnailImageId)
                .Select(image => image.Url)
                .FirstOrDefaultAsync(ct)
            ?? await _db.ProductImages
                .Where(image => image.ProductId == product.Id)
                .OrderBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Select(image => image.Url)
                .FirstOrDefaultAsync(ct);
    }
}
