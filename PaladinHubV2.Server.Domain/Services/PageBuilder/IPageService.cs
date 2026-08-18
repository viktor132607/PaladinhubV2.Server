using System.Threading.Tasks;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public interface IPageService
	{
		Task<ContentPage?> GetByRouteAsync(string section, string slug);

		// старият, без concurrency (оставяме за съвместимост)
		Task<bool> UpdateLayoutAsync(int id, string jsonLayout, string updatedBy);

		// нов: с валидация и concurrency token; връща новия RowVersion (или null при not found)
		Task<(bool ok, byte[]? newRowVersion)> UpdateLayoutSafeAsync(int id, string jsonLayout, byte[] rowVersion, string updatedBy);

		Task<ContentPage?> GetByIdAsync(int id);
	}
}
