namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed partial class CartSessionService
	{
		private static bool TryNormalizeOwner(
			string? userId,
			out string ownerKey)
		{
			ownerKey = userId?.Trim() ?? string.Empty;
			return !string.IsNullOrWhiteSpace(ownerKey);
		}

		private static bool TryNormalizeProductId(
			string? productId,
			out Guid productGuid,
			out string normalizedProductId)
		{
			normalizedProductId = string.Empty;

			if (!Guid.TryParse(productId?.Trim(), out productGuid))
			{
				return false;
			}

			normalizedProductId = productGuid.ToString();
			return true;
		}

		private static bool IsAnonymousOwner(string ownerKey)
		{
			return ownerKey.StartsWith(
				AnonymousOwnerPrefix,
				StringComparison.OrdinalIgnoreCase);
		}
	}
}
