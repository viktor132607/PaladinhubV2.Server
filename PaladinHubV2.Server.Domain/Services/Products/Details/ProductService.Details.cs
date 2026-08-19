using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public partial class ProductService
	{
		public async Task<ProductDetailsViewModel?> GetDetailsAsync(string id, CancellationToken ct)
		{
			var p = await context.Products.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == id, ct);
			if (p == null) return null;

			var extras = await context.ProductImages
				.Where(i => i.ProductId == id)
				.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
				.Select(i => new ProductDetailsViewModel.ImageItem { Id = i.Id, Url = i.Url })
				.ToListAsync(ct);

			var thumbUrl =
				await context.ProductImages
					.Where(i => i.ProductId == id && i.Id == p.ThumbnailImageId)
					.Select(i => i.Url)
					.FirstOrDefaultAsync(ct)
				?? await context.ProductImages
					.Where(i => i.ProductId == id)
					.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
					.Select(i => i.Url)
					.FirstOrDefaultAsync(ct);

			return new ProductDetailsViewModel
			{
				Id = p.Id,
				Name = p.Name,
				Price = p.Price,
				ImageUrl = thumbUrl,
				Category = p.Category,
				Description = p.Description,
				Images = extras
			};
		}

		public async Task<ProductDetailsViewModel?> GetDetailsAsync(string id, string? currentUserId, bool isAdmin, CancellationToken ct)
		{
			var p = await context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
			if (p == null) return null;

			var extras = await context.ProductImages
				.Where(i => i.ProductId == id)
				.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
				.Select(i => new ProductDetailsViewModel.ImageItem { Id = i.Id, Url = i.Url })
				.ToListAsync(ct);

			var thumbUrl =
				await context.ProductImages
					.Where(i => i.ProductId == id && i.Id == p.ThumbnailImageId)
					.Select(i => i.Url)
					.FirstOrDefaultAsync(ct)
				?? await context.ProductImages
					.Where(i => i.ProductId == id)
					.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
					.Select(i => i.Url)
					.FirstOrDefaultAsync(ct);

			var reviewRows =
				await (from r in context.ProductReviews
					   where r.ProductId == id
					   join u in context.Users on r.UserId equals u.Id into gj
					   from u in gj.DefaultIfEmpty()
					   orderby r.CreatedAt descending
					   select new
					   {
						   r.Id,
						   r.UserId,
						   r.Rating,
						   r.Content,
						   r.CreatedAt,
						   Display = u != null ? (u.Email ?? u.UserName) : r.UserId
					   }).ToListAsync(ct);

			var avg = reviewRows.Count == 0 ? 0 : reviewRows.Average(x => x.Rating);

			var similar = await context.Products.AsNoTracking()
				.Where(x => x.Category == p.Category && x.Id != p.Id)
				.OrderByDescending(x => x.Id)
				.Take(8)
				.Select(x => new SimilarVm
				{
					Id = x.Id,
					Name = x.Name,
					Price = x.Price,
					ImageUrl =
						context.ProductImages
							.Where(i => i.ProductId == x.Id && i.Id == x.ThumbnailImageId)
							.Select(i => i.Url)
							.FirstOrDefault()
						?? context.ProductImages
							.Where(i => i.ProductId == x.Id)
							.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
							.Select(i => i.Url)
							.FirstOrDefault()
				})
				.ToListAsync(ct);

			return new ProductDetailsViewModel
			{
				Id = p.Id,
				Name = p.Name,
				Price = p.Price,
				ImageUrl = thumbUrl,
				Category = p.Category,
				Description = p.Description,
				AverageRating = Math.Round(avg, 1),
				ReviewsCount = reviewRows.Count,
				Reviews = reviewRows.Select(x => new ReviewVm
				{
					Id = x.Id,
					UserName = x.Display,
					Rating = x.Rating,
					Content = x.Content,
					CreatedAt = x.CreatedAt,
					CanDelete = isAdmin || (currentUserId != null && x.UserId == currentUserId)
				}).ToList(),
				Similar = similar,
				Images = extras
			};
		}
	}
}
