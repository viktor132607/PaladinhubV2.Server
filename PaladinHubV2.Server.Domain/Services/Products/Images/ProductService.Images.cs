using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public partial class ProductService
	{
		public async Task<bool> AddImageAsync(string productId, string url, int? sortOrder, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(url)) return false;

			context.ProductImages.Add(new ProductImage
			{
				ProductId = productId,
				Url = url.Trim(),
				SortOrder = sortOrder ?? 0
			});
			await context.SaveChangesAsync(ct);
			return true;
		}

		public async Task<bool> RemoveImageAsync(int imageId, CancellationToken ct)
		{
			var img = await context.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId, ct);
			if (img == null) return false;

			context.ProductImages.Remove(img);
			await context.SaveChangesAsync(ct);
			return true;
		}
	}
}
