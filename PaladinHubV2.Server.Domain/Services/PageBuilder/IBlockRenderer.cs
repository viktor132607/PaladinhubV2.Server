using System.Threading.Tasks;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public interface IBlockRenderer
	{
		Task<string> RenderAsync(string jsonLayout);

		Task<string> RenderBlockAsync(string blockJson)
		{
			return RenderAsync($"[{blockJson}]");
		}
	}
}
