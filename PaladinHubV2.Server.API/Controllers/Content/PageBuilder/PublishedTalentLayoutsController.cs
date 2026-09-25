using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Presets;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder;

[ApiController]
[Route("api/talent-layouts")]
public sealed class PublishedTalentLayoutsController(IDataPresetService presets) : ControllerBase
{
    private static readonly Dictionary<string, string[]> Layouts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["holy"] = ["holy-herald", "holy-lightsmith"],
        ["protection"] = ["protection-lightsmith", "protection-templar"],
        ["retribution"] = ["retribution-herald", "retribution-templar"]
    };

    [HttpGet("{spec}")]
    public async Task<IActionResult> Get(string spec, CancellationToken cancellationToken)
    {
        if (!Layouts.TryGetValue(spec, out var allowed)) return NotFound();

        var rows = await presets.ListAsync("talent-layout", ct: cancellationToken);
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.OrderByDescending(row => row.UpdatedAt).ThenByDescending(row => row.Id))
        {
            if (!string.Equals(row.Section, spec, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var document = JsonDocument.Parse(row.JsonQuery);
                var root = document.RootElement;
                if (!root.TryGetProperty("published", out var published) || published.ValueKind != JsonValueKind.True ||
                    !root.TryGetProperty("targetKey", out var keyValue) || keyValue.ValueKind != JsonValueKind.String ||
                    !root.TryGetProperty("trees", out var trees) || trees.ValueKind != JsonValueKind.Array || trees.GetArrayLength() == 0)
                    continue;

                var key = keyValue.GetString()!;
                if (allowed.Contains(key, StringComparer.OrdinalIgnoreCase) && !result.ContainsKey(key))
                    result[key] = trees.Clone();
            }
            catch (JsonException)
            {
                // An invalid draft must not break the published guide.
            }
        }

        return Ok(result);
    }
}
