using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products;

public sealed class ProductMutationService : IProductMutationService
{
    private readonly AppDbContext _db;

    public ProductMutationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CreateProductViewModel> CreateAsync(
        CreateProductViewModel model)
    {
        if (model is null)
        {
            return null!;
        }

        if (await _db.Products.AnyAsync(
                product => product.Name == model.Name))
        {
            return null!;
        }

        var product = new Product(model.Name, model.Price)
        {
            Category = model.Category,
            Description = model.Description
        };

        await _db.Products.AddAsync(product);
        await _db.SaveChangesAsync();

        List<ProductImageInputModel> imageInputs =
            NormalizeImages(model.Images);

        List<ProductImage> images = imageInputs
            .Select(input => NewImage(product.Id, input))
            .ToList();

        if (images.Count > 0)
        {
            _db.ProductImages.AddRange(images);
            await _db.SaveChangesAsync();

            ProductImage chosen =
                ChooseCreatedThumbnail(
                    images,
                    model.ThumbnailIndex);

            product.ThumbnailImageId = chosen.Id;
            await _db.SaveChangesAsync();
        }

        return model;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        Product? product = await _db.Products
            .FirstOrDefaultAsync(item => item.Id == id);

        if (product is null)
        {
            return false;
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(
        EditProductViewModel model,
        CancellationToken ct = default)
    {
        Product? product = await _db.Products
            .Include(item => item.Images)
            .FirstOrDefaultAsync(
                item => item.Id == model.Id,
                ct);

        if (product is null)
        {
            return false;
        }

        bool nameTaken = await _db.Products.AnyAsync(
            item =>
                item.Id != model.Id &&
                item.Name == model.Name,
            ct);

        if (nameTaken)
        {
            return false;
        }

        product.Name = model.Name;
        product.Price = model.Price;
        product.Category = model.Category;
        product.Description = model.Description;

        List<ProductImageInputModel> incoming =
            NormalizeImages(model.Images);

        HashSet<int> retainedIds = incoming
            .Where(input => input.Id.HasValue)
            .Select(input => input.Id!.Value)
            .ToHashSet();

        List<ProductImage> persistedImages =
            product.Images
                .Where(image => image.Id > 0)
                .ToList();

        foreach (ProductImageInputModel input in incoming)
        {
            ProductImage? existing = input.Id.HasValue
                ? persistedImages.FirstOrDefault(
                    image => image.Id == input.Id.Value)
                : null;

            if (existing is not null)
            {
                Apply(existing, input);
            }
            else
            {
                product.Images.Add(
                    NewImage(product.Id, input));
            }
        }

        List<ProductImage> removed = persistedImages
            .Where(image => !retainedIds.Contains(image.Id))
            .ToList();

        if (removed.Count > 0)
        {
            _db.ProductImages.RemoveRange(removed);
        }

        await _db.SaveChangesAsync(ct);

        ProductImage? thumbnail =
            await ResolveUpdatedThumbnailAsync(
                product.Id,
                model,
                ct);

        product.ThumbnailImageId = thumbnail?.Id;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AddImageAsync(
        string productId,
        string url,
        int? sortOrder,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(productId) ||
            string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        _db.ProductImages.Add(new ProductImage
        {
            ProductId = productId,
            Url = url.Trim(),
            SortOrder = sortOrder ?? 0
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveImageAsync(
        int imageId,
        CancellationToken ct)
    {
        ProductImage? image = await _db.ProductImages
            .FirstOrDefaultAsync(
                item => item.Id == imageId,
                ct);

        if (image is null)
        {
            return false;
        }

        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static List<ProductImageInputModel> NormalizeImages(
        IEnumerable<ProductImageInputModel>? images)
    {
        return (images ?? [])
            .Where(image =>
                !string.IsNullOrWhiteSpace(image.Url))
            .OrderBy(image => image.SortOrder)
            .ToList();
    }

    private static ProductImage NewImage(
        string productId,
        ProductImageInputModel input)
    {
        return new ProductImage
        {
            ProductId = productId,
            Url = input.Url.Trim(),
            SortOrder = input.SortOrder,
            AltText = string.IsNullOrWhiteSpace(input.AltText)
                ? null
                : input.AltText.Trim()
        };
    }

    private static void Apply(
        ProductImage image,
        ProductImageInputModel input)
    {
        image.Url = input.Url.Trim();
        image.SortOrder = input.SortOrder;
        image.AltText = string.IsNullOrWhiteSpace(input.AltText)
            ? null
            : input.AltText.Trim();
    }

    private static ProductImage ChooseCreatedThumbnail(
        IReadOnlyList<ProductImage> images,
        int? thumbnailIndex)
    {
        IOrderedEnumerable<ProductImage> ordered =
            images
                .OrderBy(image => image.SortOrder)
                .ThenBy(image => image.Id);

        if (thumbnailIndex.HasValue &&
            thumbnailIndex.Value >= 0)
        {
            return ordered
                .Skip(thumbnailIndex.Value)
                .FirstOrDefault()
                ?? ordered.First();
        }

        return ordered.First();
    }

    private async Task<ProductImage?> ResolveUpdatedThumbnailAsync(
        string productId,
        EditProductViewModel model,
        CancellationToken ct)
    {
        ProductImage? chosen = null;

        if (model.ThumbnailImageId.HasValue)
        {
            chosen = await _db.ProductImages
                .FirstOrDefaultAsync(
                    image =>
                        image.ProductId == productId &&
                        image.Id == model.ThumbnailImageId.Value,
                    ct);
        }

        if (chosen is null &&
            model.ThumbnailIndex.HasValue &&
            model.ThumbnailIndex.Value >= 0)
        {
            chosen = await _db.ProductImages
                .Where(image => image.ProductId == productId)
                .OrderBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .Skip(model.ThumbnailIndex.Value)
                .FirstOrDefaultAsync(ct);
        }

        return chosen
            ?? await _db.ProductImages
                .Where(image => image.ProductId == productId)
                .OrderBy(image => image.SortOrder)
                .ThenBy(image => image.Id)
                .FirstOrDefaultAsync(ct);
    }
}
