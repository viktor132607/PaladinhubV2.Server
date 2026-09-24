using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class PersistentCartSessionService :
    IPersistentCartSessionService
{
    private readonly ICartService _cartService;
    private readonly AppDbContext _db;

    public PersistentCartSessionService(
        ICartService cartService,
        AppDbContext db)
    {
        _cartService = cartService;
        _db = db;
    }

    public Task<bool> AddProductAsync(
        string ownerKey,
        string productId) =>
        _cartService.AddProduct(productId, ownerKey);

    public Task<bool> IncreaseProductAsync(
        string ownerKey,
        string productId) =>
        _cartService.IncreaseProduct(productId, ownerKey);

    public Task<bool> DecreaseProductAsync(
        string ownerKey,
        string productId) =>
        _cartService.DecreaseProduct(productId, ownerKey);

    public Task<bool> RemoveProductAsync(
        string ownerKey,
        string productId) =>
        _cartService.RemoveProduct(productId, ownerKey);

    public async Task<int> GetCountAsync(
        string ownerKey,
        CancellationToken cancellationToken)
    {
        int? count = await _db.Carts
            .AsNoTracking()
            .Where(cart =>
                cart.UserId == ownerKey &&
                !cart.IsArchived)
            .SelectMany(cart => cart.CartProducts)
            .Select(cartProduct =>
                (int?)cartProduct.Quantity)
            .SumAsync(cancellationToken);

        return count ?? 0;
    }
}
