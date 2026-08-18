using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.Checkout
{
	public class PaymentVM
	{
		[Required]
		public PaymentMethod Method { get; set; } = PaymentMethod.Card;
	}
}