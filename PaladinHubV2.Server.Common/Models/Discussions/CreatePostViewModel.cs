using System.ComponentModel.DataAnnotations;
namespace PaladinHub.Models.Discussions
{
	public class CreatePostViewModel
	{
		[Required, MaxLength(120)] public string Title { get; set; } = default!;
		[Required] public string Content { get; set; } = default!;
	}
}