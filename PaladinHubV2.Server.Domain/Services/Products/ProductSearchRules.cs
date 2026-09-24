using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products;

internal static class ProductSearchRules
{
    internal static void NormalizePriceBounds(ProductQueryOptions options)
    {
        if (options.MinPrice.HasValue &&
            options.MaxPrice.HasValue &&
            options.MaxPrice.Value < options.MinPrice.Value)
        {
            (options.MinPrice, options.MaxPrice) =
                (options.MaxPrice, options.MinPrice);
        }
    }

    internal static IReadOnlyList<ProductPriceBand> BuildPriceBands(
        ProductQueryOptions options)
    {
        List<ProductPriceBand> bands = [];

        foreach (string? token in options.PriceRanges ?? [])
        {
            switch ((token ?? string.Empty).Trim())
            {
                case "0-100":
                    bands.Add(new ProductPriceBand(0m, 100m));
                    break;
                case "100-200":
                    bands.Add(new ProductPriceBand(100m, 200m));
                    break;
                case "200-500":
                    bands.Add(new ProductPriceBand(200m, 500m));
                    break;
                case "500+":
                    bands.Add(new ProductPriceBand(500m, null));
                    break;
            }
        }

        if (options.MinPrice.HasValue || options.MaxPrice.HasValue)
        {
            bands.Add(new ProductPriceBand(
                options.MinPrice,
                options.MaxPrice));
        }

        return bands;
    }

    internal static int Page(ProductQueryOptions options) =>
        Math.Max(1, options.Page);

    internal static int PageSize(ProductQueryOptions options) =>
        Math.Clamp(options.PageSize, 1, 200);
}
