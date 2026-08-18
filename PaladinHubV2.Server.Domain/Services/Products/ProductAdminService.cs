using Microsoft.AspNetCore.Mvc.Rendering;
using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public sealed class ProductAdminService : IProductAdminService
	{
		private readonly IProductService _products;

		public ProductAdminService(IProductService products)
		{
			_products = products;
		}

		public async Task<CreateProductViewModel> BuildCreateModelAsync(
			CancellationToken cancellationToken = default)
		{
			var categories =
				await _products.GetAllCategoriesAsync(cancellationToken);

			var model = new CreateProductViewModel
			{
				Category = "Other",
				CategorySelectList = categories.Select(category =>
					new SelectListItem
					{
						Value = category,
						Text = category
					})
			};

			model.Images.Add(new ProductImageInputModel
			{
				Url = string.Empty,
				SortOrder = 0
			});

			return model;
		}

		public void Normalize(CreateProductViewModel model)
		{
			ArgumentNullException.ThrowIfNull(model);

			if (!string.IsNullOrWhiteSpace(model.NewCategory))
			{
				model.Category = model.NewCategory.Trim();
			}
		}

		public void Normalize(EditProductViewModel model)
		{
			ArgumentNullException.ThrowIfNull(model);

			if (!string.IsNullOrWhiteSpace(model.NewCategory))
			{
				model.Category = model.NewCategory.Trim();
			}
		}

		public Task<CreateProductViewModel?> CreateAsync(
			CreateProductViewModel model,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(model);
			return CreateCoreAsync(model);
		}

		public async Task<EditProductViewModel?> GetForEditAsync(
			string id,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return null;
			}

			EditProductViewModel? model =
				await _products.GetForEditAsync(
					id.Trim(),
					cancellationToken);

			if (model == null)
			{
				return null;
			}

			var categories =
				await _products.GetAllCategoriesAsync(cancellationToken);

			model.CategorySelectList = categories.Select(category =>
				new SelectListItem
				{
					Value = category,
					Text = category,
					Selected = string.Equals(
						category,
						model.Category,
						StringComparison.OrdinalIgnoreCase)
				});

			model.Images ??= new();

			if (model.Images.Count == 0)
			{
				model.Images.Add(new ProductImageInputModel
				{
					Url = string.Empty,
					SortOrder = 0
				});
			}

			return model;
		}

		public Task<bool> UpdateAsync(
			EditProductViewModel model,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(model);
			return _products.UpdateAsync(model, cancellationToken);
		}

		public Task<bool> DeleteAsync(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return Task.FromResult(false);
			}

			return _products.Delete(id.Trim());
		}

		private async Task<CreateProductViewModel?> CreateCoreAsync(
			CreateProductViewModel model)
		{
			CreateProductViewModel created =
				await _products.Create(model);

			return created;
		}
	}
}
