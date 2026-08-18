namespace PaladinHub.Models.Talents
{
	public class TalentTreeViewModel
	{
		/// <summary>
		/// Уникален ключ за дървото (напр. "holy", "protection", "holy-herald").
		/// </summary>
		public string Key { get; set; } = string.Empty;

		/// <summary>
		/// Заглавие, което се показва над дървото.
		/// </summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>
		/// Hero дърво ли е (true) или стандартно class/spec (false).
		/// </summary>
		public bool IsHero { get; set; } = false;

		/// <summary>
		/// Лимит на точки за това дърво. Ако е null – няма лимит.
		/// </summary>
		public int? MaxPoints { get; set; }

		/// <summary>
		/// Нодовете в дървото (позиция, форма, активност, зависимости и т.н.).
		/// </summary>
		public List<TalentNodeViewModel> Nodes { get; set; } = new();

		/// <summary>
		/// Ребрата (връзки) между нодовете.
		/// </summary>
		public List<TalentEdgeViewModel> Edges { get; set; } = new();
	}
}
