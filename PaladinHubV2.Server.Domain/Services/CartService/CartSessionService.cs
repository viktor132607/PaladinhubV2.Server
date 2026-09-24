using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class CartSessionService : ICartSessionService
{
    private readonly ICartSessionRequestPolicy _policy;
    private readonly IAnonymousCartSessionService _anonymous;
    private readonly IPersistentCartSessionService _persistent;
    private readonly ICartSessionLifecycleService _lifecycle;

    public CartSessionService(
        ICartSessionRequestPolicy policy,
        IAnonymousCartSessionService anonymous,
        IPersistentCartSessionService persistent,
        ICartSessionLifecycleService lifecycle)
    {
        _policy = policy;
        _anonymous = anonymous;
        _persistent = persistent;
        _lifecycle = lifecycle;
    }

    public Task<bool> AddProduct(
        string productId,
        string userId,
        CancellationToken cancellationToken) =>
        RouteProductMutationAsync(
            productId,
            userId,
            cancellationToken,
            _anonymous.AddProductAsync,
            _persistent.AddProductAsync);

    public Task<bool> IncreaseProduct(
        string productId,
        string userId,
        CancellationToken cancellationToken) =>
        RouteProductMutationAsync(
            productId,
            userId,
            cancellationToken,
            _anonymous.IncreaseProductAsync,
            _persistent.IncreaseProductAsync);

    public Task<bool> DecreaseProduct(
        string productId,
        string userId,
        CancellationToken cancellationToken) =>
        RouteProductMutationAsync(
            productId,
            userId,
            cancellationToken,
            _anonymous.DecreaseProductAsync,
            _persistent.DecreaseProductAsync);

    public Task<bool> RemoveProduct(
        string productId,
        string userId,
        CancellationToken cancellationToken) =>
        RouteProductMutationAsync(
            productId,
            userId,
            cancellationToken,
            _anonymous.RemoveProductAsync,
            _persistent.RemoveProductAsync);

    public Task ArchiveAndClear(
        User user,
        CancellationToken cancellationToken) =>
        _lifecycle.ArchiveAndClearAsync(
            user,
            cancellationToken);

    public Task CleanAndClear(
        User user,
        CancellationToken cancellationToken) =>
        _lifecycle.CleanAndClearAsync(
            user,
            cancellationToken);

    public Task SyncRedisToPersistent(
        User user,
        CancellationToken cancellationToken) =>
        _lifecycle.SyncStoreToPersistentAsync(
            user,
            cancellationToken);

    public async Task<int> GetCount(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!_policy.TryNormalizeOwner(
                userId,
                out string ownerKey))
        {
            return 0;
        }

        return _policy.IsAnonymousOwner(ownerKey)
            ? await _anonymous.GetCountAsync(
                ownerKey,
                cancellationToken)
            : await _persistent.GetCountAsync(
                ownerKey,
                cancellationToken);
    }

    private async Task<bool> RouteProductMutationAsync(
        string productId,
        string userId,
        CancellationToken cancellationToken,
        Func<string, Guid, CancellationToken, Task<bool>>
            anonymousOperation,
        Func<string, string, Task<bool>>
            persistentOperation)
    {
        if (!_policy.TryNormalizeOwner(
                userId,
                out string ownerKey) ||
            !_policy.TryNormalizeProduct(
                productId,
                out Guid productGuid,
                out string normalizedProductId))
        {
            return false;
        }

        return _policy.IsAnonymousOwner(ownerKey)
            ? await anonymousOperation(
                ownerKey,
                productGuid,
                cancellationToken)
            : await persistentOperation(
                ownerKey,
                normalizedProductId);
    }
}
