using Microsoft.AspNetCore.Mvc.Rendering;
using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public sealed class ProductAdminFormService : IProductAdminFormService
	{
		private readonly IProductService _productService;

		public ProductAdminFormService(
			IProductService productService)
		{
			_productService = productService;
		}

		public async Task<CreateProductViewModel>
			BuildCreateModelAsync()
		{
			List<string> categories =
				await _productService.GetCategories();

			var model = new CreateProductViewModel
			{
				Category = "Other",
				CategorySelectList = categories.Select(
					category => new SelectListItem
					{
						Value = category,
						Text = category
					})
			};

			model.Images.Add(
				new ProductImageInputModel
				{
					Url = string.Empty,
					SortOrder = 0
				});

			return model;
		}

		public async Task<EditProductViewModel?>
			BuildEditModelAsync(
				string id,
				CancellationToken cancellationToken = default)
		{
			EditProductViewModel? model =
				await _productService.GetForEditAsync(
					id,
					cancellationToken);

			if (model == null)
			{
				return null;
			}

			List<string> categories =
				await _productService.GetCategories();

			model.CategorySelectList =
				categories.Select(category =>
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
				model.Images.Add(
					new ProductImageInputModel
					{
						Url = string.Empty,
						SortOrder = 0
					});
			}

			return model;
		}

		public void ApplyNewCategory(
			CreateProductViewModel model)
		{
			if (!string.IsNullOrWhiteSpace(
					model.NewCategory))
			{
				model.Category = model.NewCategory.Trim();
			}
		}

		public void ApplyNewCategory(
			EditProductViewModel model)
		{
			if (!string.IsNullOrWhiteSpace(
					model.NewCategory))
			{
				model.Category = model.NewCategory.Trim();
			}
		}
	}
}
