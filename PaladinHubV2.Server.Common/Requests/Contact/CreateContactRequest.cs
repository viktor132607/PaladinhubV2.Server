using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Contact;

public sealed class CreateContactRequest
{
	[Required]
	[StringLength(120, MinimumLength = 2)]
	public required string Name { get; set; }

	[Required]
	[EmailAddress]
	[StringLength(254)]
	public required string Email { get; set; }

	[Required]
	[StringLength(20, MinimumLength = 7)]
	[RegularExpression(
		@"^\+?[0-9\s().-]{7,20}$",
		ErrorMessage = "The phone number is invalid.")]
	public required string Phone { get; set; }

	[StringLength(160)]
	public string? Subject { get; set; }

	[Required]
	[StringLength(4000, MinimumLength = 10)]
	public required string Message { get; set; }
}
