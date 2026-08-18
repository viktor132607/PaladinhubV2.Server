using PaladinHubV2.Common.Requests.Image;

namespace PaladinHubV2.Common.Requests.Product;

public class CreateProductRequest
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string MainImageUrl { get; set; }
    public decimal RegularPrice { get; set; }
    public byte DiscountPercentage { get; set; }
    public decimal DiscountedPrice { get; set; }
    public uint Quantity { get; set; }
    public Guid CategoryId { get; set; }
    public ICollection<CreateImageRequest> SecondaryImages { get; set; }
}

