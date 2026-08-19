using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public partial class ProductService
	{
		public async Task<ICollection<ProductViewModel>> GetAll()
			=> await context.Products.AsNoTracking()
				.Select(p => new ProductViewModel
				{
					Id = p.Id,
					Name = p.Name,
					Price = p.Price,
					ImageUrl =
						context.ProductImages
							.Where(i => i.ProductId == p.Id && i.Id == p.ThumbnailImageId)
							.Select(i => i.Url)
							.FirstOrDefault()
						?? context.ProductImages
							.Where(i => i.ProductId == p.Id)
							.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
							.Select(i => i.Url)
							.FirstOrDefault(),
					Category = p.Category,
					Description = p.Description
				})
				.ToListAsync();

		public async Task<List<string>> GetAllCategoriesAsync(CancellationToken ct = default)
			=> await context.Products.AsNoTracking()
				.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c))
				.Distinct().OrderBy(c => c).ToListAsync(ct);

		public async Task<List<string>> GetCategories()
			=> await context.Products.AsNoTracking()
				.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c))
				.Distinct().OrderBy(c => c).ToListAsync();

		private sealed class AggRow
		{
			public Product P { get; set; } = default!;
			public double Avg { get; set; }
			public int Cnt { get; set; }
		}

		public async Task<PagedResult<ProductListItem>> QueryAsync(ProductQueryOptions options, CancellationToken ct = default)
		{
			IQueryable<Product> baseQ = context.Products.AsNoTracking();

			if (options.MinPrice.HasValue && options.MaxPrice.HasValue &&
				options.MaxPrice.Value < options.MinPrice.Value)
			{
				(options.MinPrice, options.MaxPrice) = (options.MaxPrice, options.MinPrice);
			}

			if (!string.IsNullOrWhiteSpace(options.Search))
			{
				var s = options.Search.Trim();
				baseQ = baseQ.Where(p =>
					EF.Functions.ILike(p.Name, $"%{s}%") ||
					(p.Description != null && EF.Functions.ILike(p.Description, $"%{s}%")) ||
					(p.Category != null && EF.Functions.ILike(p.Category, $"%{s}%"))
				);
			}

			if (options.Categories is { Count: > 0 })
			{
				var cats = options.Categories.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
				if (cats.Count > 0) baseQ = baseQ.Where(p => cats.Contains(p.Category));
			}

			var bands = new List<(decimal? Min, decimal? Max)>();

			if (options.PriceRanges is { Count: > 0 })
			{
				foreach (var token in options.PriceRanges)
				{
					switch ((token ?? "").Trim())
					{
						case "0-100": bands.Add((0m, 100m)); break;
						case "100-200": bands.Add((100m, 200m)); break;
						case "200-500": bands.Add((200m, 500m)); break;
						case "500+": bands.Add((500m, null)); break;
					}
				}
			}

			if (options.MinPrice.HasValue || options.MaxPrice.HasValue)
			{
				var mn = options.MinPrice;
				var mx = options.MaxPrice;
				if (mn.HasValue && mx.HasValue && mx < mn) (mn, mx) = (mx, mn);
				bands.Add((mn, mx));
			}

			if (bands.Count > 0)
			{
				var pParam = Expression.Parameter(typeof(Product), "p");
				var priceProp = Expression.Property(pParam, nameof(Product.Price));

				Expression? orExpr = null;

				foreach (var (Min, Max) in bands)
				{
					Expression bandExpr;
					if (Min.HasValue && Max.HasValue)
					{
						var ge = Expression.GreaterThanOrEqual(priceProp, Expression.Constant(Min.Value, typeof(decimal)));
						var le = Expression.LessThanOrEqual(priceProp, Expression.Constant(Max.Value, typeof(decimal)));
						bandExpr = Expression.AndAlso(ge, le);
					}
					else if (Min.HasValue)
					{
						bandExpr = Expression.GreaterThanOrEqual(priceProp, Expression.Constant(Min.Value, typeof(decimal)));
					}
					else if (Max.HasValue)
					{
						bandExpr = Expression.LessThanOrEqual(priceProp, Expression.Constant(Max.Value, typeof(decimal)));
					}
					else continue;

					orExpr = orExpr == null ? bandExpr : Expression.OrElse(orExpr, bandExpr);
				}

				if (orExpr != null)
				{
					var lambda = Expression.Lambda<Func<Product, bool>>(orExpr, pParam);
					baseQ = baseQ.Where(lambda);
				}
			}

			var agg = context.ProductReviews
				.GroupBy(r => r.ProductId)
				.Select(g => new { ProductId = g.Key, Avg = g.Average(x => (double)x.Rating), Cnt = g.Count() });

			var withAgg =
				from p in baseQ
				join a in agg on p.Id equals a.ProductId into gj
				from a in gj.DefaultIfEmpty()
				select new AggRow
				{
					P = p,
					Avg = (double?)a.Avg ?? 0.0,
					Cnt = (int?)a.Cnt ?? 0
				};

			if (options.MinRating is int minR && minR >= 1 && minR <= 5)
			{
				double lower = minR;
				double upper = (minR < 5) ? minR + 0.49 : 5;
				withAgg = withAgg.Where(x => x.Avg >= lower && x.Avg <= upper);
			}

			IOrderedQueryable<AggRow> ordered = options.SortBy switch
			{
				ProductSortBy.Price =>
					options.Desc ? withAgg.OrderByDescending(x => x.P.Price).ThenBy(x => x.P.Name)
								 : withAgg.OrderBy(x => x.P.Price).ThenBy(x => x.P.Name),

				ProductSortBy.Newest =>
					options.Desc ? withAgg.OrderByDescending(x => x.P.Id)
								 : withAgg.OrderBy(x => x.P.Id),

				ProductSortBy.Name =>
					options.Desc ? withAgg.OrderByDescending(x => x.P.Name)
								 : withAgg.OrderBy(x => x.P.Name),

				ProductSortBy.Rating =>
					options.Desc ? withAgg.OrderByDescending(x => x.Avg).ThenByDescending(x => x.Cnt).ThenBy(x => x.P.Name)
								 : withAgg.OrderBy(x => x.Avg).ThenBy(x => x.P.Name),

				ProductSortBy.MostReviewed =>
					options.Desc ? withAgg.OrderByDescending(x => x.Cnt).ThenByDescending(x => x.Avg).ThenBy(x => x.P.Name)
								 : withAgg.OrderBy(x => x.Cnt).ThenBy(x => x.P.Name),

				_ =>
					options.Desc ? withAgg.OrderByDescending(x => x.P.Name).ThenByDescending(x => x.P.Id)
								 : withAgg.OrderBy(x => x.P.Name).ThenBy(x => x.P.Id)
			};

			var total = await ordered.CountAsync(ct);

			var pageSize = Math.Clamp(options.PageSize, 1, 200);
			var page = Math.Max(1, options.Page);
			var skip = (page - 1) * pageSize;

			var items = await ordered
				.Skip(skip).Take(pageSize)
				.Select(x => new ProductListItem
				{
					Id = x.P.Id,
					Name = x.P.Name,
					Price = x.P.Price,
					ImageUrl =
						context.ProductImages
							.Where(i => i.ProductId == x.P.Id && i.Id == x.P.ThumbnailImageId)
							.Select(i => i.Url)
							.FirstOrDefault()
						?? context.ProductImages
							.Where(i => i.ProductId == x.P.Id)
							.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
							.Select(i => i.Url)
							.FirstOrDefault(),
					Category = x.P.Category,
					AverageRating = (decimal)x.Avg,
					ReviewsCount = x.Cnt
				})
				.ToListAsync(ct);

			return new PagedResult<ProductListItem>
			{
				Items = items,
				Page = page,
				PageSize = pageSize,
				TotalItems = total
			};
		}
	}
}
