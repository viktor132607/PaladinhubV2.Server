using System.Threading.Tasks;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Pages
{
	public interface IPageService
	{
		Task<ContentPage?> GetByRouteAsync(string section, string slug);
		Task<bool> UpdateLayoutAsync(int id, string jsonLayout, string updatedBy);
	}
}
