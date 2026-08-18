using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.Checkout
{
	public class CardVM
	{
		[StringLength(80)]
		public string Cardholder { get; set; } = "";

		[Required, CreditCard, Display(Name = "Card number")]
		public string CardNumber { get; set; } = "";

		[Required, StringLength(5, MinimumLength = 5, ErrorMessage = "MM/YY")]
		public string Expiry { get; set; } = "";

		[Required, StringLength(4, MinimumLength = 3)]
		public string Cvc { get; set; } = "";
	}
}
