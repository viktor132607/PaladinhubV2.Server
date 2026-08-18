using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[AllowAnonymous]
	[Route("api/merchandise")]
	[Route("Merchandise")]
	public sealed class MerchandiseController : ControllerBase
	{
		private readonly IProductService _productService;
		private readonly AppDbContext _db;

		public MerchandiseController(
			IProductService productService,
			AppDbContext db)
		{
			_productService = productService;
			_db = db;
		}

		[HttpGet]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public Task<IActionResult> Index(
			[FromQuery] ProductQueryOptions options,
			CancellationToken cancellationToken = default)
		{
			return Merchandise(options, cancellationToken);
		}

		[HttpGet("List")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Merchandise(
			[FromQuery] ProductQueryOptions options,
			CancellationToken cancellationToken = default)
		{
			Normalize(options);

			var products = await _productService.QueryAsync(
				options,
				cancellationToken);

			var categories =
				await _productService.GetAllCategoriesAsync(
					cancellationToken);

			var ratingBuckets =
				await BuildRatingBucketsAsync(
					options,
					cancellationToken);

			var model = new MerchandisePageViewModel
			{
				Query = options,
				Products = products,
				AllCategories = categories,
				RatingAtLeast = ratingBuckets
			};

			return Ok(model);
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
				if (options.MinRating < 1)
				{
					options.MinRating = 1;
				}

				if (options.MinRating > 5)
				{
					options.MinRating = 5;
				}
			}
		}

		private async Task<Dictionary<int, int>>
			BuildRatingBucketsAsync(
				ProductQueryOptions options,
				CancellationToken cancellationToken)
		{
			var query = _db.Products
				.AsNoTracking()
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(options.Search))
			{
				var search = options.Search.Trim();

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
				var categories = options.Categories
					.Where(category =>
						!string.IsNullOrWhiteSpace(category))
					.ToList();

				if (categories.Count > 0)
				{
					query = query.Where(product =>
						categories.Contains(product.Category));
				}
			}

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

			var reviewAggregates = _db.ProductReviews
				.GroupBy(review => review.ProductId)
				.Select(group => new
				{
					ProductId = group.Key,
					Average = group.Average(
						review => (double)review.Rating)
				});

			var productsWithAverage =
				from product in query
				join aggregate in reviewAggregates
					on product.Id equals aggregate.ProductId
					into aggregateGroup
				from aggregate in aggregateGroup.DefaultIfEmpty()
				select new
				{
					Average =
						(double?)aggregate.Average ??
						0.0
				};

			var averages =
				await productsWithAverage.ToListAsync(
					cancellationToken);

			var buckets = new Dictionary<int, int>
			{
				[1] = 0,
				[2] = 0,
				[3] = 0,
				[4] = 0,
				[5] = 0
			};

			foreach (var row in averages)
			{
				var average = row.Average;

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
	}
}
