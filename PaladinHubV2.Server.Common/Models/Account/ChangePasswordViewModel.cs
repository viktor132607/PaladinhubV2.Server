using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.Account
{
	public class ChangePasswordViewModel
	{
		[EmailAddress]
		[Display(Name = "Email (optional)")]
		public string? Email { get; set; }  

		[Required(ErrorMessage = "Current password is required.")]
		[DataType(DataType.Password)]
		[Display(Name = "Current Password")]
		public string OldPassword { get; set; } = default!;

		[Required(ErrorMessage = "New password is required.")]
		[StringLength(40, MinimumLength = 8,
			ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.")]
		[DataType(DataType.Password)]
		[Display(Name = "New Password")]
		public string NewPassword { get; set; } = default!;

		[Required(ErrorMessage = "Confirm password is required.")]
		[DataType(DataType.Password)]
		[Display(Name = "Confirm New Password")]
		[Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
		public string ConfirmNewPassword { get; set; } = default!;
	}
}
