using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public partial class ProductService
	{
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
	}
}
