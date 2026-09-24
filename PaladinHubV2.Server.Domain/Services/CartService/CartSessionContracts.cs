using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public interface ICartSessionRequestPolicy
{
    bool TryNormalizeOwner(string? userId, out string ownerKey);

    bool TryNormalizeProduct(
        string? productId,
        out Guid productGuid,
        out string normalizedProductId);

    bool IsAnonymousOwner(string ownerKey);
}

public interface IAnonymousCartSessionService
{
    Task<bool> AddProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken);

    Task<bool> IncreaseProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken);

    Task<bool> DecreaseProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken);

    Task<bool> RemoveProductAsync(
        string ownerKey,
        Guid productId,
        CancellationToken cancellationToken);

    Task<int> GetCountAsync(
        string ownerKey,
        CancellationToken cancellationToken);
}

public interface IPersistentCartSessionService
{
    Task<bool> AddProductAsync(
        string ownerKey,
        string productId);

    Task<bool> IncreaseProductAsync(
        string ownerKey,
        string productId);

    Task<bool> DecreaseProductAsync(
        string ownerKey,
        string productId);

    Task<bool> RemoveProductAsync(
        string ownerKey,
        string productId);

    Task<int> GetCountAsync(
        string ownerKey,
        CancellationToken cancellationToken);
}

public interface ICartSessionLifecycleService
{
    Task ArchiveAndClearAsync(
        User user,
        CancellationToken cancellationToken);

    Task CleanAndClearAsync(
        User user,
        CancellationToken cancellationToken);

    Task SyncStoreToPersistentAsync(
        User user,
        CancellationToken cancellationToken);
}
