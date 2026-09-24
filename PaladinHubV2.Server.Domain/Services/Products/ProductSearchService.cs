using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products;

public sealed class ProductSearchService : IProductSearchService
{
    private readonly AppDbContext _db;

    public ProductSearchService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ProductListItem>> QueryAsync(
        ProductQueryOptions options,
        CancellationToken ct = default)
    {
        ProductSearchRules.NormalizePriceBounds(options);

        IQueryable<Product> products =
            _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            string pattern =
                $"%{options.Search.Trim().ToLower()}%";

            products = products.Where(product =>
                EF.Functions.Like(
                    product.Name.ToLower(),
                    pattern) ||
                (product.Description != null &&
                 EF.Functions.Like(
                     product.Description.ToLower(),
                     pattern)) ||
                (product.Category != null &&
                 EF.Functions.Like(
                     product.Category.ToLower(),
                     pattern)));
        }

        if (options.Categories is { Count: > 0 })
        {
            List<string> categories = options.Categories
                .Where(category =>
                    !string.IsNullOrWhiteSpace(category))
                .ToList();

            if (categories.Count > 0)
            {
                products = products.Where(product =>
                    categories.Contains(product.Category));
            }
        }

        IReadOnlyList<ProductPriceBand> bands =
            ProductSearchRules.BuildPriceBands(options);

        if (bands.Count > 0)
        {
            products = products.Where(
                BuildPricePredicate(bands));
        }

        IQueryable<ProductAggregateRow> aggregate =
            BuildAggregate(products);

        if (options.MinRating is int minRating &&
            minRating >= 1 &&
            minRating <= 5)
        {
            double lower = minRating;
            double upper =
                minRating < 5
                    ? minRating + 0.49
                    : 5;

            aggregate = aggregate.Where(item =>
                item.AverageRating >= lower &&
                item.AverageRating <= upper);
        }

        IOrderedQueryable<ProductAggregateRow> ordered =
            Order(aggregate, options);

        int total = await ordered.CountAsync(ct);
        int pageSize = ProductSearchRules.PageSize(options);
        int page = ProductSearchRules.Page(options);
        int skip = (page - 1) * pageSize;

        List<ProductListItem> items = await ordered
            .Skip(skip)
            .Take(pageSize)
            .Select(item => new ProductListItem
            {
                Id = item.Product.Id,
                Name = item.Product.Name,
                Price = item.Product.Price,
                ImageUrl =
                    _db.ProductImages
                        .Where(image =>
                            image.ProductId == item.Product.Id &&
                            image.Id == item.Product.ThumbnailImageId)
                        .Select(image => image.Url)
                        .FirstOrDefault()
                    ?? _db.ProductImages
                        .Where(image =>
                            image.ProductId == item.Product.Id)
                        .OrderBy(image => image.SortOrder)
                        .ThenBy(image => image.Id)
                        .Select(image => image.Url)
                        .FirstOrDefault(),
                Category = item.Product.Category,
                AverageRating = (decimal)item.AverageRating,
                ReviewsCount = item.ReviewsCount
            })
            .ToListAsync(ct);

        return new PagedResult<ProductListItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    private IQueryable<ProductAggregateRow> BuildAggregate(
        IQueryable<Product> products)
    {
        var reviews = _db.ProductReviews
            .GroupBy(review => review.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Average = group.Average(
                    review => (double)review.Rating),
                Count = group.Count()
            });

        return
            from product in products
            join review in reviews
                on product.Id equals review.ProductId
                into reviewGroup
            from review in reviewGroup.DefaultIfEmpty()
            select new ProductAggregateRow
            {
                Product = product,
                AverageRating =
                    (double?)review.Average ?? 0.0,
                ReviewsCount =
                    (int?)review.Count ?? 0
            };
    }

    private static Expression<Func<Product, bool>>
        BuildPricePredicate(
            IReadOnlyList<ProductPriceBand> bands)
    {
        ParameterExpression product =
            Expression.Parameter(typeof(Product), "product");
        MemberExpression price =
            Expression.Property(product, nameof(Product.Price));

        Expression? combined = null;

        foreach (ProductPriceBand band in bands)
        {
            Expression current;

            if (band.Min.HasValue && band.Max.HasValue)
            {
                Expression minimum =
                    Expression.GreaterThanOrEqual(
                        price,
                        Expression.Constant(
                            band.Min.Value,
                            typeof(decimal)));
                Expression maximum =
                    Expression.LessThanOrEqual(
                        price,
                        Expression.Constant(
                            band.Max.Value,
                            typeof(decimal)));
                current =
                    Expression.AndAlso(minimum, maximum);
            }
            else if (band.Min.HasValue)
            {
                current =
                    Expression.GreaterThanOrEqual(
                        price,
                        Expression.Constant(
                            band.Min.Value,
                            typeof(decimal)));
            }
            else
            {
                current =
                    Expression.LessThanOrEqual(
                        price,
                        Expression.Constant(
                            band.Max!.Value,
                            typeof(decimal)));
            }

            combined = combined is null
                ? current
                : Expression.OrElse(combined, current);
        }

        return Expression.Lambda<Func<Product, bool>>(
            combined!,
            product);
    }

    private static IOrderedQueryable<ProductAggregateRow> Order(
        IQueryable<ProductAggregateRow> query,
        ProductQueryOptions options)
    {
        return options.SortBy switch
        {
            ProductSortBy.Price =>
                options.Desc
                    ? query
                        .OrderByDescending(item => item.Product.Price)
                        .ThenBy(item => item.Product.Name)
                    : query
                        .OrderBy(item => item.Product.Price)
                        .ThenBy(item => item.Product.Name),

            ProductSortBy.Newest =>
                options.Desc
                    ? query.OrderByDescending(item => item.Product.Id)
                    : query.OrderBy(item => item.Product.Id),

            ProductSortBy.Name =>
                options.Desc
                    ? query.OrderByDescending(item => item.Product.Name)
                    : query.OrderBy(item => item.Product.Name),

            ProductSortBy.Rating =>
                options.Desc
                    ? query
                        .OrderByDescending(item => item.AverageRating)
                        .ThenByDescending(item => item.ReviewsCount)
                        .ThenBy(item => item.Product.Name)
                    : query
                        .OrderBy(item => item.AverageRating)
                        .ThenBy(item => item.Product.Name),

            ProductSortBy.MostReviewed =>
                options.Desc
                    ? query
                        .OrderByDescending(item => item.ReviewsCount)
                        .ThenByDescending(item => item.AverageRating)
                        .ThenBy(item => item.Product.Name)
                    : query
                        .OrderBy(item => item.ReviewsCount)
                        .ThenBy(item => item.Product.Name),

            _ =>
                options.Desc
                    ? query
                        .OrderByDescending(item => item.Product.Name)
                        .ThenByDescending(item => item.Product.Id)
                    : query
                        .OrderBy(item => item.Product.Name)
                        .ThenBy(item => item.Product.Id)
        };
    }

    private sealed class ProductAggregateRow
    {
        public Product Product { get; init; } = null!;
        public double AverageRating { get; init; }
        public int ReviewsCount { get; init; }
    }
}
