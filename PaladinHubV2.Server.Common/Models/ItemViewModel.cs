namespace PaladinHub.Models
{
	public class ItemViewModel
	{
		public int Id { get; set; }
		public string? Name { get; set; }
		public string? Icon { get; set; }
		public string? SecondIcon { get; set; }
		public string? Description { get; set; }
		public string? Irl { get; set; }
		public int? ItemLevel { get; set; }
		public int? RequiredLevel { get; set; }
		public string? Quality { get; set; }
	}
}
