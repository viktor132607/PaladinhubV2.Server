using PaladinHubV2.Common.Responses.Order;
using PaladinHubV2.Common.Responses.Review;
using PaladinHubV2.Common.Responses.Users;

namespace PaladinHubV2.Common.Responses.Gdpr;

public class GdprExportResponse
{
    public DateTime RequestedAtUtc { get; set; }

    public UserResponse? User { get; set; }

    public ICollection<OrderResponse> Orders { get; set; } = new List<OrderResponse>();

    public ICollection<Guid> WishlistProductIds { get; set; } = new List<Guid>();

    public ICollection<ReviewResponse> Reviews { get; set; } = new List<ReviewResponse>();
}
