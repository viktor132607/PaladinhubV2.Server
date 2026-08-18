using PaladinHubV2.Common.Responses.Image;
using PaladinHubV2.Core.StaticClasses;

namespace PaladinHubV2.Common.Responses.Product;

public class ProductResponse
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }
    
    public required string MainImageUrl { get; set; }

    public decimal RegularPrice { get; set; }

    public string CurrencyCode { get; set; } = CurrencyDefaults.Code;
    
    public byte DiscountPercentage { get; set; } 
    
    public decimal DiscountedPrice { get; set; }
    
    public double Rating { get; set; } 

    public uint Quantity { get; set; }

    public Guid CategoryId { get; set; }
    
    public required string CategoryName { get; set; }
    
    public ICollection<ImageResponse> SecondaryImages { get; set; } 
}
