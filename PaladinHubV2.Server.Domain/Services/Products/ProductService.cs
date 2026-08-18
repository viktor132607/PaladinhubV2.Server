using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public class ProductService : IProductService
	{
		private readonly AppDbContext context;
		public ProductService(AppDbContext context) => this.context = context;

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

		public async Task<CreateProductViewModel> Create(CreateProductViewModel model)
		{
			if (model == null) return null!;
			if (await context.Products.AnyAsync(x => x.Name == model.Name)) return null!;

			var entity = new Product(model.Name, model.Price)
			{
				Category = model.Category,
				Description = model.Description
			};

			await context.Products.AddAsync(entity);
			await context.SaveChangesAsync();

			var imagesInput = (model.Images ?? new List<ProductImageInputModel>())
				.Where(i => !string.IsNullOrWhiteSpace(i.Url))
				.OrderBy(i => i.SortOrder)
				.ToList();

			var images = new List<ProductImage>();
			foreach (var img in imagesInput)
			{
				images.Add(new ProductImage
				{
					ProductId = entity.Id,
					Url = img.Url!.Trim(),
					SortOrder = img.SortOrder,
					AltText = string.IsNullOrWhiteSpace(img.AltText) ? null : img.AltText!.Trim()
				});
			}

			if (images.Count > 0)
			{
				context.ProductImages.AddRange(images);
				await context.SaveChangesAsync();

				ProductImage chosen;
				if (model.ThumbnailIndex.HasValue && model.ThumbnailIndex.Value >= 0)
				{
					chosen = images
						.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
						.Skip(model.ThumbnailIndex.Value)
						.FirstOrDefault() ?? images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).First();
				}
				else
				{
					chosen = images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).First();
				}

				entity.ThumbnailImageId = chosen.Id;
				await context.SaveChangesAsync();
			}

			return model;
		}

		public async Task<MyCartViewModel> GetMyProducts(User user)
		{
			var vm = new MyCartViewModel
			{
				MyProducts = new List<ProductViewModel>(),
				TotalPrice = 0m
			};

			if (user == null) return vm;

			var cart = await context.Carts
				.AsNoTracking()
				.FirstOrDefaultAsync(c => c.UserId == user.Id);

			if (cart == null) return vm;

			var myCartProducts = await context.CartProducts
				.Include(x => x.Product)
				.Where(x => x.CartId == cart.Id)
				.ToListAsync();

			if (myCartProducts.Count == 0) return vm;

			var productIds = myCartProducts.Select(cp => cp.ProductId).Distinct().ToList();

			var thumbs = await context.Products
				.Where(p => productIds.Contains(p.Id))
				.Select(p => new
				{
					p.Id,
					ThumbUrl =
						context.ProductImages
							.Where(i => i.ProductId == p.Id && i.Id == p.ThumbnailImageId)
							.Select(i => i.Url)
							.FirstOrDefault()
						?? context.ProductImages
							.Where(i => i.ProductId == p.Id)
							.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
							.Select(i => i.Url)
							.FirstOrDefault()
				})
				.ToListAsync();

			var thumbMap = thumbs.ToDictionary(x => x.Id, x => x.ThumbUrl);

			foreach (var cp in myCartProducts)
			{
				if (!vm.MyProducts.Any(x => x.Id == cp.ProductId))
				{
					vm.MyProducts.Add(new ProductViewModel
					{
						Id = cp.ProductId,
						Name = cp.Product?.Name ?? string.Empty,
						Price = cp.Product?.Price ?? 0m,
						ImageUrl = thumbMap.GetValueOrDefault(cp.ProductId),
						Category = cp.Product?.Category,
						Description = cp.Product?.Description,
						Quantity = cp.Quantity,
						CartId = cart.Id,
						Cart = null
					});
				}
				vm.TotalPrice += (cp.Product?.Price ?? 0m) * cp.Quantity;
			}
			return vm;
		}

		public async Task<bool> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return false;
			var entity = await context.Products.FirstOrDefaultAsync(p => p.Id == id);
			if (entity == null) return false;

			context.Products.Remove(entity);
			await context.SaveChangesAsync();
			return true;
		}

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

		public async Task<EditProductViewModel?> GetForEditAsync(string id, CancellationToken ct = default)
		{
			var p = await context.Products
				.AsNoTracking()
				.Include(x => x.Images)
				.FirstOrDefaultAsync(x => x.Id == id, ct);

			if (p == null) return null;

			var vm = new EditProductViewModel
			{
				Id = p.Id,
				Name = p.Name,
				Price = p.Price,
				Category = p.Category,
				Description = p.Description,
				ThumbnailImageId = p.ThumbnailImageId,
				Images = p.Images
					.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
					.Select(i => new ProductImageInputModel
					{
						Id = i.Id,
						Url = i.Url,
						SortOrder = i.SortOrder,
						AltText = i.AltText
					})
					.ToList()
			};

			if (vm.ThumbnailImageId.HasValue)
			{
				var ordered = p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
				var idx = ordered.FindIndex(i => i.Id == vm.ThumbnailImageId.Value);
				vm.ThumbnailIndex = idx >= 0 ? idx : null;
			}

			return vm;
		}

		public async Task<bool> UpdateAsync(EditProductViewModel model, CancellationToken ct = default)
		{
			var entity = await context.Products
				.Include(p => p.Images)
				.FirstOrDefaultAsync(p => p.Id == model.Id, ct);

			if (entity == null) return false;

			var nameTaken = await context.Products
				.AnyAsync(p => p.Id != model.Id && p.Name == model.Name, ct);
			if (nameTaken) return false;

			entity.Name = model.Name;
			entity.Price = model.Price;
			entity.Category = model.Category;
			entity.Description = model.Description;

			var incoming = (model.Images ?? new List<ProductImageInputModel>())
				.Where(x => !string.IsNullOrWhiteSpace(x.Url))
				.ToList();

			foreach (var im in incoming)
			{
				if (im.Id.HasValue)
				{
					var existing = entity.Images.FirstOrDefault(x => x.Id == im.Id.Value);
					if (existing != null)
					{
						existing.Url = im.Url!.Trim();
						existing.SortOrder = im.SortOrder;
						existing.AltText = string.IsNullOrWhiteSpace(im.AltText) ? null : im.AltText!.Trim();
					}
					else
					{
						entity.Images.Add(new ProductImage
						{
							ProductId = entity.Id,
							Url = im.Url!.Trim(),
							SortOrder = im.SortOrder,
							AltText = string.IsNullOrWhiteSpace(im.AltText) ? null : im.AltText!.Trim()
						});
					}
				}
				else
				{
					entity.Images.Add(new ProductImage
					{
						ProductId = entity.Id,
						Url = im.Url!.Trim(),
						SortOrder = im.SortOrder,
						AltText = string.IsNullOrWhiteSpace(im.AltText) ? null : im.AltText!.Trim()
					});
				}
			}

			var incomingIds = incoming.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
			var toRemove = entity.Images.Where(x => !incomingIds.Contains(x.Id)).ToList();
			if (toRemove.Count > 0)
			{
				context.ProductImages.RemoveRange(toRemove);
			}

			await context.SaveChangesAsync(ct);

			ProductImage? chosen = null;

			if (model.ThumbnailImageId.HasValue)
			{
				chosen = await context.ProductImages
					.Where(i => i.ProductId == entity.Id && i.Id == model.ThumbnailImageId.Value)
					.FirstOrDefaultAsync(ct);
			}

			if (chosen == null && model.ThumbnailIndex.HasValue && model.ThumbnailIndex.Value >= 0)
			{
				chosen = await context.ProductImages
					.Where(i => i.ProductId == entity.Id)
					.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
					.Skip(model.ThumbnailIndex.Value)
					.FirstOrDefaultAsync(ct);
			}

			chosen ??= await context.ProductImages
				.Where(i => i.ProductId == entity.Id)
				.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
				.FirstOrDefaultAsync(ct);

			entity.ThumbnailImageId = chosen?.Id;
			await context.SaveChangesAsync(ct);
			return true;
		}

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

		public async Task<bool> AddReviewAsync(AddReviewInput input, string userId, CancellationToken ct)
		{
			var hasInCart = await context.CartProducts
				.AnyAsync(cp => cp.ProductId == input.ProductId && cp.Cart.UserId == userId, ct);
			if (!hasInCart) return false;

			var exists = await context.ProductReviews
				.AnyAsync(r => r.ProductId == input.ProductId && r.UserId == userId, ct);
			if (exists) return false;

			var entity = new ProductReview
			{
				ProductId = input.ProductId,
				UserId = userId,
				Rating = input.Rating,
				Content = string.IsNullOrWhiteSpace(input.Content) ? null : input.Content.Trim()
			};

			context.ProductReviews.Add(entity);
			await context.SaveChangesAsync(ct);
			return true;
		}

		public async Task<bool> DeleteReviewAsync(int reviewId, string userId, bool isAdmin, CancellationToken ct)
		{
			var r = await context.ProductReviews.FirstOrDefaultAsync(x => x.Id == reviewId, ct);
			if (r == null) return false;
			if (!isAdmin && r.UserId != userId) return false;

			context.ProductReviews.Remove(r);
			await context.SaveChangesAsync(ct);
			return true;
		}

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
