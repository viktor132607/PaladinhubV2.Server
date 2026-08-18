using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Review;

public class UpdateReviewRequest
{
    [Required]
    public required Guid Id { get; set; }
    
    [Required]
    public required string Content { get; set; }
    
    [Required]
    public required byte Rating { get; set; }
}
