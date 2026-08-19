using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public sealed class MerchandiseService : IMerchandiseService
	{
		private readonly AppDbContext _db;

		public MerchandiseService(AppDbContext db)
		{
			_db = db;
		}

		public async Task<MerchandisePageViewModel> GetPageAsync(
			ProductQueryOptions options,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(options);

			Normalize(options);

			IQueryable<Product> filteredProducts =
				ApplyProductFilters(
					_db.Products.AsNoTracking(),
					options,
					includePriceRanges: true);

			var reviewAggregates = _db.ProductReviews
				.AsNoTracking()
				.GroupBy(review => review.ProductId)
				.Select(group => new
				{
					ProductId = group.Key,
					Average = group.Average(
						review => (double)review.Rating),
					Count = group.Count()
				});

			IQueryable<MerchandiseProductRow> productsWithRatings =
				from product in filteredProducts
				join aggregate in reviewAggregates
					on product.Id equals aggregate.ProductId
					into aggregateGroup
				from aggregate in aggregateGroup.DefaultIfEmpty()
				select new MerchandiseProductRow
				{
					Product = product,
					AverageRating =
						(double?)aggregate.Average ?? 0.0,
					ReviewsCount =
						(int?)aggregate.Count ?? 0
				};

			if (options.MinRating is int ratingBand)
			{
				double lower = ratingBand;
				double upper =
					ratingBand < 5
						? ratingBand + 0.49
						: 5.0;

				productsWithRatings =
					productsWithRatings.Where(row =>
						row.AverageRating >= lower &&
						row.AverageRating <= upper);
			}

			IOrderedQueryable<MerchandiseProductRow> ordered =
				OrderProducts(productsWithRatings, options);

			int totalItems =
				await ordered.CountAsync(cancellationToken);

			int skip =
				(options.Page - 1) * options.PageSize;

			List<ProductListItem> products =
				await ordered
					.Skip(skip)
					.Take(options.PageSize)
					.Select(row => new ProductListItem
					{
						Id = row.Product.Id,
						Name = row.Product.Name,
						Price = row.Product.Price,
						ImageUrl =
							_db.ProductImages
								.Where(image =>
									image.ProductId == row.Product.Id &&
									image.Id == row.Product.ThumbnailImageId)
								.Select(image => image.Url)
								.FirstOrDefault()
							?? _db.ProductImages
								.Where(image =>
									image.ProductId == row.Product.Id)
								.OrderBy(image => image.SortOrder)
								.ThenBy(image => image.Id)
								.Select(image => image.Url)
								.FirstOrDefault(),
						Category = row.Product.Category,
						AverageRating =
							(decimal)row.AverageRating,
						ReviewsCount = row.ReviewsCount
					})
					.ToListAsync(cancellationToken);

			List<string> categories =
				await _db.Products
					.AsNoTracking()
					.Select(product => product.Category)
					.Where(category =>
						!string.IsNullOrWhiteSpace(category))
					.Distinct()
					.OrderBy(category => category)
					.ToListAsync(cancellationToken);

			Dictionary<int, int> ratingBuckets =
				await BuildRatingBucketsAsync(
					options,
					cancellationToken);

			return new MerchandisePageViewModel
			{
				Query = options,
				Products = new PagedResult<ProductListItem>
				{
					Items = products,
					Page = options.Page,
					PageSize = options.PageSize,
					TotalItems = totalItems
				},
				AllCategories = categories,
				RatingAtLeast = ratingBuckets
			};
		}

		private static void Normalize(ProductQueryOptions options)
		{
			if (options.Page <= 0)
			{
				options.Page = 1;
			}

			if (options.PageSize <= 0)
			{
				options.PageSize = 20;
			}

			if (options.PageSize > 200)
			{
				options.PageSize = 200;
			}

			if (!Enum.IsDefined(
					typeof(ProductSortBy),
					options.SortBy))
			{
				options.SortBy = ProductSortBy.Relevance;
			}

			if (options.MinRating.HasValue)
			{
				options.MinRating =
					Math.Clamp(
						options.MinRating.Value,
						1,
						5);
			}
		}

		private static IQueryable<Product> ApplyProductFilters(
			IQueryable<Product> query,
			ProductQueryOptions options,
			bool includePriceRanges)
		{
			if (!string.IsNullOrWhiteSpace(options.Search))
			{
				string search = options.Search.Trim();

				query = query.Where(product =>
					EF.Functions.ILike(
						product.Name,
						$"%{search}%") ||
					(
						product.Description != null &&
						EF.Functions.ILike(
							product.Description,
							$"%{search}%")
					) ||
					(
						product.Category != null &&
						EF.Functions.ILike(
							product.Category,
							$"%{search}%")
					));
			}

			if (options.Categories is { Count: > 0 })
			{
				List<string> categories = options.Categories
					.Where(category =>
						!string.IsNullOrWhiteSpace(category))
					.ToList();

				if (categories.Count > 0)
				{
					query = query.Where(product =>
						categories.Contains(product.Category));
				}
			}

			if (!includePriceRanges)
			{
				if (options.MinPrice.HasValue)
				{
					query = query.Where(product =>
						product.Price >= options.MinPrice.Value);
				}

				if (options.MaxPrice.HasValue)
				{
					query = query.Where(product =>
						product.Price <= options.MaxPrice.Value);
				}

				return query;
			}

			List<(decimal? Min, decimal? Max)> priceBands =
				BuildPriceBands(options);

			if (priceBands.Count == 0)
			{
				return query;
			}

			ParameterExpression productParameter =
				Expression.Parameter(
					typeof(Product),
					"product");

			MemberExpression priceProperty =
				Expression.Property(
					productParameter,
					nameof(Product.Price));

			Expression? combined = null;

			foreach ((decimal? min, decimal? max) in priceBands)
			{
				Expression? band =
					BuildPriceBandExpression(
						priceProperty,
						min,
						max);

				if (band == null)
				{
					continue;
				}

				combined = combined == null
					? band
					: Expression.OrElse(combined, band);
			}

			if (combined == null)
			{
				return query;
			}

			Expression<Func<Product, bool>> predicate =
				Expression.Lambda<Func<Product, bool>>(
					combined,
					productParameter);

			return query.Where(predicate);
		}

		private static List<(decimal? Min, decimal? Max)>
			BuildPriceBands(ProductQueryOptions options)
		{
			var bands =
				new List<(decimal? Min, decimal? Max)>();

			if (options.PriceRanges is { Count: > 0 })
			{
				foreach (string? token in options.PriceRanges)
				{
					switch ((token ?? string.Empty).Trim())
					{
						case "0-100":
							bands.Add((0m, 100m));
							break;
						case "100-200":
							bands.Add((100m, 200m));
							break;
						case "200-500":
							bands.Add((200m, 500m));
							break;
						case "500+":
							bands.Add((500m, null));
							break;
					}
				}
			}

			if (
				options.MinPrice.HasValue ||
				options.MaxPrice.HasValue)
			{
				decimal? min = options.MinPrice;
				decimal? max = options.MaxPrice;

				if (
					min.HasValue &&
					max.HasValue &&
					max.Value < min.Value)
				{
					(min, max) = (max, min);
				}

				bands.Add((min, max));
			}

			return bands;
		}

		private static Expression? BuildPriceBandExpression(
			MemberExpression price,
			decimal? min,
			decimal? max)
		{
			if (min.HasValue && max.HasValue)
			{
				return Expression.AndAlso(
					Expression.GreaterThanOrEqual(
						price,
						Expression.Constant(min.Value)),
					Expression.LessThanOrEqual(
						price,
						Expression.Constant(max.Value)));
			}

			if (min.HasValue)
			{
				return Expression.GreaterThanOrEqual(
					price,
					Expression.Constant(min.Value));
			}

			if (max.HasValue)
			{
				return Expression.LessThanOrEqual(
					price,
					Expression.Constant(max.Value));
			}

			return null;
		}

		private async Task<Dictionary<int, int>>
			BuildRatingBucketsAsync(
				ProductQueryOptions options,
				CancellationToken cancellationToken)
		{
			IQueryable<Product> filteredProducts =
				ApplyProductFilters(
					_db.Products.AsNoTracking(),
					options,
					includePriceRanges: false);

			var reviewAggregates = _db.ProductReviews
				.AsNoTracking()
				.GroupBy(review => review.ProductId)
				.Select(group => new
				{
					ProductId = group.Key,
					Average = group.Average(
						review => (double)review.Rating)
				});

			List<double> averages =
				await (
					from product in filteredProducts
					join aggregate in reviewAggregates
						on product.Id equals aggregate.ProductId
						into aggregateGroup
					from aggregate in aggregateGroup.DefaultIfEmpty()
					select (double?)aggregate.Average ?? 0.0
				)
				.ToListAsync(cancellationToken);

			var buckets = new Dictionary<int, int>
			{
				[1] = 0,
				[2] = 0,
				[3] = 0,
				[4] = 0,
				[5] = 0
			};

			foreach (double average in averages)
			{
				if (average >= 5.0)
				{
					buckets[5]++;
				}
				else if (average >= 4.0 && average <= 4.49)
				{
					buckets[4]++;
				}
				else if (average >= 3.0 && average <= 3.49)
				{
					buckets[3]++;
				}
				else if (average >= 2.0 && average <= 2.49)
				{
					buckets[2]++;
				}
				else if (average >= 1.0 && average <= 1.49)
				{
					buckets[1]++;
				}
			}

			return buckets;
		}

		private static IOrderedQueryable<MerchandiseProductRow>
			OrderProducts(
				IQueryable<MerchandiseProductRow> query,
				ProductQueryOptions options)
		{
			return options.SortBy switch
			{
				ProductSortBy.Price =>
					options.Desc
						? query
							.OrderByDescending(row => row.Product.Price)
							.ThenBy(row => row.Product.Name)
						: query
							.OrderBy(row => row.Product.Price)
							.ThenBy(row => row.Product.Name),

				ProductSortBy.Newest =>
					options.Desc
						? query.OrderByDescending(row => row.Product.Id)
						: query.OrderBy(row => row.Product.Id),

				ProductSortBy.Name =>
					options.Desc
						? query.OrderByDescending(row => row.Product.Name)
						: query.OrderBy(row => row.Product.Name),

				ProductSortBy.Rating =>
					options.Desc
						? query
							.OrderByDescending(row => row.AverageRating)
							.ThenByDescending(row => row.ReviewsCount)
							.ThenBy(row => row.Product.Name)
						: query
							.OrderBy(row => row.AverageRating)
							.ThenBy(row => row.Product.Name),

				ProductSortBy.MostReviewed =>
					options.Desc
						? query
							.OrderByDescending(row => row.ReviewsCount)
							.ThenByDescending(row => row.AverageRating)
							.ThenBy(row => row.Product.Name)
						: query
							.OrderBy(row => row.ReviewsCount)
							.ThenBy(row => row.Product.Name),

				_ =>
					options.Desc
						? query
							.OrderByDescending(row => row.Product.Name)
							.ThenByDescending(row => row.Product.Id)
						: query
							.OrderBy(row => row.Product.Name)
							.ThenBy(row => row.Product.Id)
			};
		}

		private sealed class MerchandiseProductRow
		{
			public Product Product { get; init; } = default!;
			public double AverageRating { get; init; }
			public int ReviewsCount { get; init; }
		}
	}
}
