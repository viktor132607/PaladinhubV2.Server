namespace PaladinHub.Models.Blocks
{
	public record ItemRef(int? Id = null, string? Name = null);
	public record SpellRef(int? Id = null, string? Name = null, string? Note = null);

	public record BuildInfo(string Name, bool IsDefault);
	public record TabPane(string Title, string? Html = null); 
	public record GearRow(string Slot, ItemRef Item, string Source);
	public record ConsumableRow(string Type, ItemRef Best, ItemRef? Alternative = null);

	public record TableColumn(string Key, string Title, string Kind = "text"); 
	public record TierEntry(string Type, int? Id = null, string? Name = null); 
	public record SwitcherOption(string Label, string? Html = null);

	public abstract class BlockVM
	{
		public string SectionId { get; set; } = $"block-{Guid.NewGuid():N}";
		public string? Title { get; set; }
	}

	public class DividerBlock : BlockVM
	{
		public string Style { get; set; } = "D1";
		public string? CssClass { get; set; }
	}

	public class PageHeaderBlock : BlockVM
	{
		public string IconUrl { get; set; } = "/images/itemIcons/inv_scroll_03.jpg";
		public bool SmallIcon { get; set; } = false;
	}

	public class HeadingBlock : BlockVM
	{
		public string Level { get; set; } = "h2";

		public string Align { get; set; } = "left";
		public string? CssClass { get; set; }

		public string Text { get; set; } = "Heading";
	}

	public class TabsBlock : BlockVM
	{
		public List<TabPane> Tabs { get; set; } = new();
	}

	public class SwitcherBlock : BlockVM
	{
		public List<SwitcherOption> Options { get; set; } = new();
	}

	public class GearTableBlock : BlockVM
	{
		public List<GearRow> Rows { get; set; } = new();
	}

	public class ConsumablesTableBlock : BlockVM
	{
		public List<ConsumableRow> Rows { get; set; } = new();
	}

	public class GenericTableBlock : BlockVM
	{
		public List<TableColumn> Columns { get; set; } = new();


		public int? PresetId { get; set; }


		public IReadOnlyList<Dictionary<string, object?>>? Rows { get; set; }
	}

	public class TierListBlock : BlockVM
	{
		public List<string> Tiers { get; set; } = new() { "S", "A", "B", "C" };
		public Dictionary<string, List<TierEntry>> ItemsByTier { get; set; } = new();
	}

	public class ColumnsTextBlock : BlockVM
	{

		public int Columns { get; set; } = 2;

		public List<string> MarkdownPerColumn { get; set; } = new();
	}

	public class TalentTreeBlock : BlockVM
	{
		public string TreeKey { get; set; } = "paladin";

		public string? Build { get; set; }
	}

	public class TalentBuildMenuBlock : BlockVM
	{
		public string TreeKey { get; set; } = "paladin";
		public List<BuildInfo> Builds { get; set; } = new();
		public string? Selected { get; set; }
	}

	public class ItemGridBlock : BlockVM
	{
		public int Columns { get; set; } = 4;
		public List<ItemRef> Items { get; set; } = new();
	}

	public class SpellListBlock : BlockVM
	{
		public List<SpellRef> Spells { get; set; } = new();
	}

	public class CalloutBlock : BlockVM
	{

		public string Variant { get; set; } = "info";

		public string Text { get; set; } = string.Empty;
	}

	public class RotationCardBlock : BlockVM
	{
		public List<SpellRef> Sequence { get; set; } = new();
	}

	public class SectionBlock : BlockVM
	{
		public string? CssClass { get; set; } = "mb-4";
		public string? Html { get; set; } 
	}
}
