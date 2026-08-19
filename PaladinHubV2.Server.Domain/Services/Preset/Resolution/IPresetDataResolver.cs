using System.Text.Json.Nodes;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public interface IPresetDataResolver
	{
		string Entity { get; }

		Task<IReadOnlyList<Dictionary<string, object?>>> ResolveAsync(
			JsonObject query,
			int? take,
			CancellationToken cancellationToken);
	}
}
