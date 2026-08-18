using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaladinHub.Models.Blocks;

public static class BlocksJson
{
	public static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		Converters = { new BlockVmJsonConverter() }
	};

	public static string Serialize(IEnumerable<BlockVM> blocks)
		=> JsonSerializer.Serialize(blocks, Options);

	public static List<BlockVM> Deserialize(string json)
		=> JsonSerializer.Deserialize<List<BlockVM>>(json, Options) ?? new();
}

public class BlockVmJsonConverter : JsonConverter<BlockVM>
{
	public override BlockVM Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using var doc = JsonDocument.ParseValue(ref reader);
		var root = doc.RootElement;
		if (!root.TryGetProperty("type", out var typeProp))
			throw new JsonException("BlockVM requires 'type'.");

		var type = typeProp.GetString()?.Trim().ToLowerInvariant() ?? "";
		BlockVM target = type switch
		{
			"divider" => new DividerBlock(),
			"pageheader" => new PageHeaderBlock(),
			"heading" => new HeadingBlock(),
			"tabs" => new TabsBlock(),
			"switcher" => new SwitcherBlock(),
			"table.gear" => new GearTableBlock(),
			"table.consumables" => new ConsumablesTableBlock(),
			"table.generic" => new GenericTableBlock(),
			"tierlist" => new TierListBlock(),
			"columnstext" => new ColumnsTextBlock(),
			"talenttree" => new TalentTreeBlock(),
			"talentbuildmenu" => new TalentBuildMenuBlock(),
			"itemgrid" => new ItemGridBlock(),
			"spelllist" => new SpellListBlock(),
			"callout" => new CalloutBlock(),
			"rotationcard" => new RotationCardBlock(),
			"section" => new SectionBlock(),
			_ => throw new JsonException($"Unknown block type '{type}'.")
		};

		var js = root.GetRawText();
		return (BlockVM)(JsonSerializer.Deserialize(js, target.GetType(), options) ?? target);
	}

	public override void Write(Utf8JsonWriter writer, BlockVM value, JsonSerializerOptions options)
	{
		var type = value switch
		{
			DividerBlock => "Divider",
			PageHeaderBlock => "PageHeader",
			HeadingBlock => "Heading",
			TabsBlock => "Tabs",
			SwitcherBlock => "Switcher",
			GearTableBlock => "Table.Gear",
			ConsumablesTableBlock => "Table.Consumables",
			GenericTableBlock => "Table.Generic",
			TierListBlock => "TierList",
			ColumnsTextBlock => "ColumnsText",
			TalentTreeBlock => "TalentTree",
			TalentBuildMenuBlock => "TalentBuildMenu",
			ItemGridBlock => "ItemGrid",
			SpellListBlock => "SpellList",
			CalloutBlock => "Callout",
			RotationCardBlock => "RotationCard",
			SectionBlock => "Section",
			_ => value.GetType().Name
		};

		using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), options));
		writer.WriteStartObject();
		writer.WriteString("type", type);
		foreach (var p in doc.RootElement.EnumerateObject())
		{
			if (p.NameEquals("type")) continue;
			p.WriteTo(writer);
		}
		writer.WriteEndObject();
	}
}
