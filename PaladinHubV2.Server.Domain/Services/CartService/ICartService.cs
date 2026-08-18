
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Carts;


namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public interface ICartService
	{
		Task<MyCartViewModel> GetCartById(Guid cartId);
		Task<ICollection<CartViewModel>> GetArchive();
		Task<bool> AddProduct(string id, string userId);
		Task<bool> IncreaseProduct(string id, string userId);
		Task<bool> DecreaseProduct(string id, string userId);
		Task<bool> RemoveProduct(string id, string userId);
		Task ArchiveCart(User user);
		Task CleanCart(User user);
	}
}
