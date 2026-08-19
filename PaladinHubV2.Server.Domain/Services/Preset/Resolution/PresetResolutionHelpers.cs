namespace PaladinHubV2.Server.Domain.Services.Presets
{
	internal static class PresetResolutionHelpers
	{
		public static Dictionary<string, object?> AnonToDict(object value) =>
			value.GetType()
				.GetProperties()
				.ToDictionary(property => property.Name, property => property.GetValue(value));
	}
}
