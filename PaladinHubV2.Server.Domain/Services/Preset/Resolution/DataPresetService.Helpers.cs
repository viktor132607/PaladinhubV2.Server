using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed partial class DataPresetService
	{
		private void InvalidateCache(DataPreset p)
		{
			// Cache keys include UpdatedAt, preserving the existing invalidation behavior.
		}
	}
}
