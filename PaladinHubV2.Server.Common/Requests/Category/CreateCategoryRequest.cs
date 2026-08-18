using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Category;

public class CreateCategoryRequest
{
    [Required]
    public required string Name { get; set; }
    
    [Required]
    public required string ImageURI { get; set; }
}
