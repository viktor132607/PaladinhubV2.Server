namespace PaladinHubV2.Common.Responses.Review;

public class ReviewResponse
{
    public required Guid Id { get; set; }
    public Guid UserId { get; set; }
    
    public required string Content { get; set; }
    
    public required byte Rating { get; set; }
    
    public required string UserNames { get; set; }
    
    public required DateTime CreatedOn { get; set; }
}
