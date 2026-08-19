using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public partial class ProductService
	{
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

		public async Task<bool> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return false;
			var entity = await context.Products.FirstOrDefaultAsync(p => p.Id == id);
			if (entity == null) return false;

			context.Products.Remove(entity);
			await context.SaveChangesAsync();
			return true;
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
	}
}
