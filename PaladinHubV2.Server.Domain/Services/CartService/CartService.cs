using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public class CartService : ICartService
	{
		private readonly AppDbContext context;
		private readonly UserManager<User> userManager;

		public CartService(AppDbContext context, UserManager<User> userManager)
		{
			this.context = context;
			this.userManager = userManager;
		}

		private async Task<Cart> GetOrCreateCartAsync(string userId)
		{
			var cart = await context.Carts.FirstOrDefaultAsync(x => x.UserId == userId);
			if (cart == null)
			{
				cart = new Cart
				{
					Id = Guid.NewGuid(),
					UserId = userId,
					UpdatedOn = DateTime.UtcNow
				};
				await context.Carts.AddAsync(cart);
				await context.SaveChangesAsync();
			}
			return cart;
		}

		public async Task<bool> AddProduct(string id, string userId)
		{
			var product = await context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
			if (product is null) return false;

			var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
			if (user is null) return false;

			var cart = await GetOrCreateCartAsync(user.Id);

			var cartProduct = await context.CartProducts
				.FirstOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == product.Id);

			if (cartProduct is null)
			{
				cartProduct = new CartProduct
				{
					CartId = cart.Id,
					ProductId = product.Id,
					Quantity = 1
				};
				await context.CartProducts.AddAsync(cartProduct);
			}
			else
			{
				cartProduct.Quantity++;
			}

			cart.UpdatedOn = DateTime.UtcNow;
			await context.SaveChangesAsync();
			return true;
		}

		public async Task ArchiveCart(User user)
		{
			if (user is null) return;

			var cart = await context.Carts.FirstOrDefaultAsync(x => x.UserId == user.Id);
			if (cart is null) return;

			cart.IsArchived = true;
			cart.OrderDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
			cart.UpdatedOn = DateTime.UtcNow;

			var newCart = new Cart
			{
				Id = Guid.NewGuid(),
				UserId = user.Id,
				UpdatedOn = DateTime.UtcNow
			};

			await context.Carts.AddAsync(newCart);
			await context.SaveChangesAsync();
		}

		public async Task CleanCart(User user)
		{
			if (user is null) return;

			var cart = await context.Carts.FirstOrDefaultAsync(c => c.UserId == user.Id);
			if (cart is null) return;

			var products = await context.CartProducts
				.Where(x => x.CartId == cart.Id)
				.ToListAsync();

			if (products.Count > 0)
				context.CartProducts.RemoveRange(products);

			cart.UpdatedOn = DateTime.UtcNow;

			await context.SaveChangesAsync();
		}

		public async Task<bool> IncreaseProduct(string id, string userId)
		{
			var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
			if (user is null) return false;

			var cart = await context.Carts.FirstOrDefaultAsync(x => x.UserId == user.Id);
			if (cart is null) return false;

			var cartProduct = await context.CartProducts
				.FirstOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == id);

			if (cartProduct is null) return false;

			cartProduct.Quantity++;
			cart.UpdatedOn = DateTime.UtcNow;

			await context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> DecreaseProduct(string id, string userId)
		{
			var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
			if (user is null) return false;

			var cart = await context.Carts.FirstOrDefaultAsync(x => x.UserId == user.Id);
			if (cart is null) return false;

			var cartProduct = await context.CartProducts
				.FirstOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == id);

			if (cartProduct is null) return false;

			if (cartProduct.Quantity > 1)
			{
				cartProduct.Quantity--;
			}
			else
			{
				context.CartProducts.Remove(cartProduct);
			}

			cart.UpdatedOn = DateTime.UtcNow;
			await context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> RemoveProduct(string id, string userId)
		{
			var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
			if (user is null) return false;

			var cart = await context.Carts.FirstOrDefaultAsync(x => x.UserId == user.Id);
			if (cart is null) return false;

			var cartProduct = await context.CartProducts
				.FirstOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == id);

			if (cartProduct is null) return false;

			context.CartProducts.Remove(cartProduct);
			cart.UpdatedOn = DateTime.UtcNow;

			await context.SaveChangesAsync();
			return true;
		}

		public async Task<ICollection<CartViewModel>> GetArchive()
		{
			var archive = await context.Carts
				.AsNoTracking()
				.Include(x => x.User)
				.Include(x => x.CartProducts)
					.ThenInclude(cp => cp.Product)
				.Where(x => x.IsArchived)
				.Select(x => new CartViewModel
				{
					Id = x.Id,
					UserId = x.UserId,
					User = x.User!,
					CartProducts = x.CartProducts,
					OrderDate = x.OrderDate ?? string.Empty,
					Products = x.CartProducts.Select(cp => cp.Product!).Where(p => p != null!).ToList()
				})
				.OrderByDescending(x => x.OrderDate)
				.ToListAsync();

			return archive;
		}

		public async Task<MyCartViewModel> GetCartById(Guid cartId)
		{
			var myCartProducts = await context.CartProducts
				.Include(x => x.Product)
				.Where(x => x.CartId == cartId)
				.ToListAsync();

			var vm = new MyCartViewModel
			{
				MyProducts = new List<ProductViewModel>(),
				TotalPrice = 0m
			};

			if (myCartProducts.Count == 0)
				return vm;

			foreach (var cp in myCartProducts)
			{
				vm.MyProducts.Add(new ProductViewModel
				{
					Id = cp.ProductId,
					Name = cp.Product?.Name ?? string.Empty,
					Price = cp.Product?.Price ?? 0m,
					Quantity = cp.Quantity,
					CartId = cp.CartId,
					Cart = null
				});

				vm.TotalPrice += (cp.Product?.Price ?? 0m) * cp.Quantity;
			}

			return vm;
		}
	}
}
