using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class AnonymousCartSessionService :
    IAnonymousCartSessionService
{
    private readonly ICartStore _cartStore;
    private readonly AppDbContext _db;

    public AnonymousCartSessionService(
        ICartStore cartStore,
        AppDbContext db)
    {
        _cartStore = cartStore;
        _db = db;
    }

    public async Task<bool> AddProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken)
    {
        string normalizedProductId =
            productId.ToString();

        bool productExists = await _db.Products
            .AsNoTracking()
            .AnyAsync(
                product =>
                    product.Id == normalizedProductId,
                cancellationToken);

        if (!productExists)
        {
            return false;
        }

        var lines = await _cartStore.GetAsync(
            ownerKey,
            cancellationToken);

        var existing = lines.FirstOrDefault(
            line => line.ProductId == productId);

        int newQuantity =
            existing is null
                ? 1
                : existing.Quantity + 1;

        await _cartStore.AddOrUpdateAsync(
            ownerKey,
            productId,
            newQuantity,
            cancellationToken);

        return true;
    }

    public async Task<bool> IncreaseProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var lines = await _cartStore.GetAsync(
            ownerKey,
            cancellationToken);

        var existing = lines.FirstOrDefault(
            line => line.ProductId == productId);

        if (existing is null || existing.Quantity <= 0)
        {
            return false;
        }

        await _cartStore.AddOrUpdateAsync(
            ownerKey,
            productId,
            existing.Quantity + 1,
            cancellationToken);

        return true;
    }

    public async Task<bool> DecreaseProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var lines = await _cartStore.GetAsync(
            ownerKey,
            cancellationToken);

        var existing = lines.FirstOrDefault(
            line => line.ProductId == productId);

        if (existing is null || existing.Quantity <= 0)
        {
            return false;
        }

        await _cartStore.AddOrUpdateAsync(
            ownerKey,
            productId,
            Math.Max(0, existing.Quantity - 1),
            cancellationToken);

        return true;
    }

    public async Task<bool> RemoveProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var lines = await _cartStore.GetAsync(
            ownerKey,
            cancellationToken);

        if (!lines.Any(
                line => line.ProductId == productId))
        {
            return false;
        }

        await _cartStore.AddOrUpdateAsync(
            ownerKey,
            productId,
            0,
            cancellationToken);

        return true;
    }

    public async Task<int> GetCountAsync(
        string ownerKey,
        CancellationToken cancellationToken)
    {
        var lines = await _cartStore.GetAsync(
            ownerKey,
            cancellationToken);

        return lines.Sum(
            line => Math.Max(0, line.Quantity));
    }
}
