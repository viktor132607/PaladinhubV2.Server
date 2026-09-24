namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class CartSessionRequestPolicy :
    ICartSessionRequestPolicy
{
    private const string AnonymousOwnerPrefix = "anon:";

    public bool TryNormalizeOwner(
        string? userId,
        out string ownerKey)
    {
        ownerKey = userId?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(ownerKey);
    }

    public bool TryNormalizeProduct(
        string? productId,
        out Guid productGuid,
        out string normalizedProductId)
    {
        normalizedProductId = string.Empty;

        if (!Guid.TryParse(
                productId?.Trim(),
                out productGuid))
        {
            return false;
        }

        normalizedProductId = productGuid.ToString();
        return true;
    }

    public bool IsAnonymousOwner(string ownerKey) =>
        ownerKey.StartsWith(
            AnonymousOwnerPrefix,
            StringComparison.OrdinalIgnoreCase);
}
