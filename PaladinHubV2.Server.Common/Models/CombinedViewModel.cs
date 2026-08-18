using Microsoft.AspNetCore.Html;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHub.Models
{
	public class CombinedViewModel
	{
		public List<Spell> Spells { get; set; } = new();
		public List<Item> Items { get; set; } = new();

		public string PageTitle { get; set; } = string.Empty;
		public string? PageText { get; set; }
		public string? CoverImage { get; set; }

		public List<NavButton> CurrentSectionButtons { get; set; } = new();
		public List<NavButton> OtherSectionButtons { get; set; } = new();

		public Dictionary<string, TalentTreeViewModel> TalentTrees { get; set; } = new();

		public List<BreadcrumbItem> Breadcrumb { get; set; } = new();
		public string? Section { get; set; }
		public string? PageHeaderTitle { get; set; }
		public string? PageHeaderSubtitle { get; set; }
		public string? PageHeaderImage { get; set; }
		public string? PageHeaderVideo { get; set; }
		public string? PageHeaderVideoPoster { get; set; }
		public List<string> PageHeaderBadges { get; set; } = new();
		public string? PageHeaderCtaText { get; set; }
		public string? PageHeaderCtaUrl { get; set; }

		public IHtmlContent this[string name]
		{
			get
			{
				if (string.IsNullOrWhiteSpace(name)) return new HtmlString("");

				var key = name.Trim();

				var item = (Items ?? new List<Item>())
					.FirstOrDefault(i => i.Name.Equals(key, System.StringComparison.OrdinalIgnoreCase));
				if (item != null)
				{
					var url = string.IsNullOrWhiteSpace(item.Url) ? "#" : item.Url!;
					var icon = string.IsNullOrWhiteSpace(item.Icon)
						? "/images/ItemIcons/placeholder.png"
						: (item.Icon.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)
							? item.Icon
							: $"/images/ItemIcons/{item.Icon}");

					return new HtmlString($@"
					<span class='item-ref'>
					  <a href='{url}' target='_blank' class='item-link'>
					    <img src='{icon}' alt='{item.Name}' width='20' height='20' />
					    <span>{item.Name}</span>
					  </a>
					</span>");
				}

				var spell = (Spells ?? new List<Spell>())
					.FirstOrDefault(s => s.Name.Equals(key, System.StringComparison.OrdinalIgnoreCase));
				if (spell != null)
				{
					var url = string.IsNullOrWhiteSpace(spell.Url) ? "#" : spell.Url!;
					var icon = string.IsNullOrWhiteSpace(spell.Icon)
						? "/images/SpellIcons/placeholder.png"
						: (spell.Icon.StartsWith("http", System.StringComparison.OrdinalIgnoreCase)
							? spell.Icon
							: $"/images/SpellIcons/{spell.Icon}");

					return new HtmlString($@"
					<span class='spell-ref'>
					  <a href='{url}' target='_blank' class='spell-link'>
					    <img src='{icon}' alt='{spell.Name}' width='20' height='20' />
					    <span>{spell.Name}</span>
					  </a>
					</span>");
				}

				return new HtmlString($"<span class='missing-ref'>{key}</span>");
			}
		}
	}

	public class BreadcrumbItem
	{
		public string Text { get; set; } = string.Empty;
		public string Url { get; set; } = "#";
	}
}
