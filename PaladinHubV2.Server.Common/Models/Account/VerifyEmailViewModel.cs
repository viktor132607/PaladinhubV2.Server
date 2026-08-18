using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.Account
{
	public class VerifyEmailViewModel
	{
		[Required(ErrorMessage = "Email is required.")]
		[EmailAddress]
		public string Email { get; set; } = string.Empty; 
	}
}
