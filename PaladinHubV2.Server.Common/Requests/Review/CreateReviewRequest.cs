using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Review;

public class CreateReviewRequest
{
    [Required]
    public required Guid ProductId { get; set; }
    
    [Required]
    public required string Content { get; set; }
    
    [Required]
    public required byte Rating { get; set; }
}
