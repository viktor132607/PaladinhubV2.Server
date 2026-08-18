using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.Checkout
{
	public class ShippingInfoVM
	{
		[Required, StringLength(80)]
		public string FullName { get; set; } = "";

		[Required, StringLength(120)]
		public string Address { get; set; } = "";

		[Required, StringLength(60)]
		public string City { get; set; } = "";

		[Required, StringLength(20)]
		public string PostalCode { get; set; } = "";

		[Required, StringLength(60)]
		public string Country { get; set; } = "Bulgaria";

		[Required, Phone]
		public string Phone { get; set; } = "";

		[EmailAddress]
		public string? Email { get; set; }
	}
}
