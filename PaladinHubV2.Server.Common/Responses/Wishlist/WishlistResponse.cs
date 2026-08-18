using PaladinHubV2.Common.Responses.Product;

namespace PaladinHubV2.Common.Responses.Wishlist;

public class WishlistResponse
{
    public ICollection<ProductsResponse> Products { get; set; } = new List<ProductsResponse>();
}
