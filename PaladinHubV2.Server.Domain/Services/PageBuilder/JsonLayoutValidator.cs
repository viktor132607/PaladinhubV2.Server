using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public interface IJsonLayoutValidator
	{
		/// <summary>Хвърля <see cref="JsonLayoutValidationException"/> ако layout-ът е невалиден.</summary>
		void ValidateOrThrow(string jsonLayout);
	}

	public sealed class JsonLayoutValidationException : Exception
	{
		public IReadOnlyList<string> Errors { get; }
		public JsonLayoutValidationException(IReadOnlyList<string> errors) : base("Layout validation failed")
			=> Errors = errors;
	}

	/// <summary>
	/// Лек валидатор: корен = масив; всеки елемент е { type: string, props?: object }.
	/// Позволени типове: pageheader|heading|tabs|table|tierlist|talenttree|markdown|callout|divider|switcher|section|itemgrid|spelllist|rotationcard|talentbuildmenu
	/// Props не се валидират дълбоко на този етап (MVP).
	/// </summary>
	public sealed class JsonLayoutValidator : IJsonLayoutValidator
	{
		private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
		{
			"pageheader","heading","tabs","table","tierlist","talenttree","markdown","callout","divider",
			"switcher","section","itemgrid","spelllist","rotationcard","talentbuildmenu"
		};

		public void ValidateOrThrow(string jsonLayout)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(jsonLayout))
			{
				// празен → позволяваме
				return;
			}

			JsonDocument doc;
			try { doc = JsonDocument.Parse(jsonLayout); }
			catch (Exception ex)
			{
				throw new JsonLayoutValidationException(new[] { $"Invalid JSON: {ex.Message}" });
			}

			using (doc)
			{
				if (doc.RootElement.ValueKind != JsonValueKind.Array)
					errors.Add("Layout root must be an array of blocks.");

				if (errors.Count == 0)
				{
					int i = 0;
					foreach (var el in doc.RootElement.EnumerateArray())
					{
						if (el.ValueKind != JsonValueKind.Object)
						{
							errors.Add($"Block[{i}] must be an object.");
							i++; continue;
						}

						if (!el.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
							errors.Add($"Block[{i}] is missing 'type' (string).");
						else
						{
							var type = typeEl.GetString() ?? "";
							if (!Allowed.Contains(type))
								errors.Add($"Block[{i}] has unsupported type '{type}'.");
						}

						// props е опционален; ако го има и е Tabs – проверяваме child blocks масив
						if (el.TryGetProperty("props", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
						{
							if (el.TryGetProperty("type", out var t2) && string.Equals(t2.GetString(), "tabs", StringComparison.OrdinalIgnoreCase))
							{
								if (propsEl.TryGetProperty("tabs", out var tabsEl) && tabsEl.ValueKind == JsonValueKind.Array)
								{
									int ti = 0;
									foreach (var tab in tabsEl.EnumerateArray())
									{
										if (tab.ValueKind != JsonValueKind.Object) { errors.Add($"tabs[{ti}] must be object"); ti++; continue; }
										if (tab.TryGetProperty("blocks", out var blocksEl))
										{
											if (blocksEl.ValueKind != JsonValueKind.Array)
												errors.Add($"tabs[{ti}].blocks must be array");
											else
											{
												int ci = 0;
												foreach (var child in blocksEl.EnumerateArray())
												{
													if (child.ValueKind != JsonValueKind.Object) { errors.Add($"tabs[{ti}].blocks[{ci}] must be object"); ci++; continue; }
													if (!child.TryGetProperty("type", out var ct) || ct.ValueKind != JsonValueKind.String)
														errors.Add($"tabs[{ti}].blocks[{ci}] missing 'type'");
													ci++;
												}
											}
										}
										ti++;
									}
								}
							}
						}

						i++;
					}
				}
			}

			if (errors.Count > 0) throw new JsonLayoutValidationException(errors);
		}
	}
}
