using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.Domain.Services.Checkout;

public sealed class CheckoutCartSnapshotProvider :
    ICheckoutCartSnapshotProvider
{
    private readonly ICartSessionService _cartSession;
    private readonly IProductService _productService;

    public CheckoutCartSnapshotProvider(
        ICartSessionService cartSession,
        IProductService productService)
    {
        _cartSession = cartSession;
        _productService = productService;
    }

    public async Task<CheckoutCartSnapshot> GetAsync(
        User user,
        CancellationToken cancellationToken)
    {
        await _cartSession.SyncRedisToPersistent(
            user,
            cancellationToken);

        var cart =
            await _productService.GetMyProducts(user);

        return new CheckoutCartSnapshot(
            cart.MyProducts?.Count ?? 0,
            cart.TotalPrice);
    }
}
