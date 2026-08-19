using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.ItemsService
{
	public interface IItemAdminService
	{
		void Normalize(Item item);
		Task<Item> CreateAsync(Item item, CancellationToken cancellationToken = default);
		Task<Item?> GetAsync(int id, CancellationToken cancellationToken = default);
		Task<Item?> UpdateAsync(int id, Item item, CancellationToken cancellationToken = default);
		Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
	}
}
