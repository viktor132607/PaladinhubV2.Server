using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.ItemsService;

public interface IItemsService
{
	Task<List<Item>> GetAllAsync();
	Task<Item?> GetByIdAsync(int id);
	Task<List<Item>> SearchAsync(string? term);
	Task<(IReadOnlyList<Item> Items, int Total)> GetPagedAsync(int page, int pageSize, string? term = null);
}
