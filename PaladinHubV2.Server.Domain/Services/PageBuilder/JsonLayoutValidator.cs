using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public interface IJsonLayoutValidator
	{
		void ValidateOrThrow(string jsonLayout);
	}

	public sealed class JsonLayoutValidationException : Exception
	{
		public IReadOnlyList<string> Errors { get; }

		public JsonLayoutValidationException(IReadOnlyList<string> errors)
			: base("Layout validation failed")
		{
			Errors = errors;
		}
	}

	public sealed class JsonLayoutValidator : IJsonLayoutValidator
	{
		private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
		{
			"talenttree.dynamic",
			"pageheader",
			"heading",
			"paragraph",
			"image",
			"tabs",
			"table",
			"table.generic",
			"table.gear",
			"table.consumables",
			"tierlist",
			"talenttree",
			"markdown",
			"callout",
			"divider",
			"switcher",
			"section",
			"columnstext",
			"itemgrid",
			"spelllist",
			"rotationcard",
			"talentbuildmenu"
		};

		public void ValidateOrThrow(string jsonLayout)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(jsonLayout))
			{
				return;
			}

			JsonDocument document;
			try
			{
				document = JsonDocument.Parse(jsonLayout);
			}
			catch (Exception exception)
			{
				throw new JsonLayoutValidationException(
					new[] { $"Invalid JSON: {exception.Message}" });
			}

			using (document)
			{
				var root = document.RootElement;
				if (root.ValueKind != JsonValueKind.Array)
				{
					errors.Add("Layout root must be an array of blocks.");
				}
				else
				{
					var index = 0;
					foreach (var block in root.EnumerateArray())
					{
						if (block.ValueKind != JsonValueKind.Object)
						{
							errors.Add($"Block[{index}] must be an object.");
							index++;
							continue;
						}

						if (!block.TryGetProperty("type", out var typeElement) ||
							typeElement.ValueKind != JsonValueKind.String)
						{
							errors.Add($"Block[{index}] is missing 'type' (string).");
						}
						else
						{
							var type = typeElement.GetString() ?? string.Empty;
							if (!Allowed.Contains(type))
							{
								errors.Add($"Block[{index}] has unsupported type '{type}'.");
							}
						}

						ValidateTabs(block, index, errors);
						index++;
					}
				}

				DynamicTalentValidator.Validate(root, errors);
			}

			if (errors.Count > 0)
			{
				throw new JsonLayoutValidationException(errors);
			}
		}

		private static void ValidateTabs(
			JsonElement block,
			int blockIndex,
			ICollection<string> errors)
		{
			if (!block.TryGetProperty("type", out var typeElement) ||
				!string.Equals(typeElement.GetString(), "tabs", StringComparison.OrdinalIgnoreCase) ||
				!block.TryGetProperty("props", out var propsElement) ||
				propsElement.ValueKind != JsonValueKind.Object ||
				!propsElement.TryGetProperty("tabs", out var tabsElement) ||
				tabsElement.ValueKind != JsonValueKind.Array)
			{
				return;
			}

			var tabIndex = 0;
			foreach (var tab in tabsElement.EnumerateArray())
			{
				if (tab.ValueKind != JsonValueKind.Object)
				{
					errors.Add($"Block[{blockIndex}].tabs[{tabIndex}] must be object.");
					tabIndex++;
					continue;
				}

				if (tab.TryGetProperty("blocks", out var blocksElement))
				{
					if (blocksElement.ValueKind != JsonValueKind.Array)
					{
						errors.Add($"Block[{blockIndex}].tabs[{tabIndex}].blocks must be array.");
					}
					else
					{
						var childIndex = 0;
						foreach (var child in blocksElement.EnumerateArray())
						{
							if (child.ValueKind != JsonValueKind.Object ||
								!child.TryGetProperty("type", out var childType) ||
								childType.ValueKind != JsonValueKind.String)
							{
								errors.Add(
									$"Block[{blockIndex}].tabs[{tabIndex}].blocks[{childIndex}] is missing 'type'.");
							}
							childIndex++;
						}
					}
				}

				tabIndex++;
			}
		}
	}
}
