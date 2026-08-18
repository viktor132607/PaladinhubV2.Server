using PaladinHubV2.Server.Data.Entities;

namespace PaladinHub.Models.Account
{
	public class MyAccountViewModel
	{
		public string Currency { get; set; } = "USD";
		public decimal Balance { get; set; }

		public List<Transaction> RecentPurchases { get; set; } = new();

		public int Page { get; set; } = 1;
		public int TotalPages { get; set; } = 1;

		public int SecurityScore { get; set; }
		public IReadOnlyList<string> SecurityTips { get; set; } = Array.Empty<string>();

		public IReadOnlyList<string> Uploads { get; set; } = Array.Empty<string>();
	}
}
