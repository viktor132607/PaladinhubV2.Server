using System;
using System.Collections.Generic;

namespace PaladinHub.Models
{
	public class TransactionHistoryItemVm
	{
		public DateTime DateUtc { get; set; }
		public string Purchase { get; set; } = string.Empty;  // напр. "Subscription - 1 Month"
		public string Total { get; set; } = string.Empty;     // напр. "BGN24.90"
		public string Status { get; set; } = string.Empty;    // "Complete" | "Pending" | ...
	}

	public class TransactionHistoryViewModel
	{
		public IReadOnlyList<TransactionHistoryItemVm> Items { get; set; }
			= Array.Empty<TransactionHistoryItemVm>();

		public int Page { get; set; }
		public int TotalPages { get; set; }
		public string Region { get; set; } = "Europe";
	}
}
