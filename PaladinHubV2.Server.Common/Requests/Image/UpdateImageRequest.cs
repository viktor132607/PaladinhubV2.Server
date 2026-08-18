using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Image;

public class UpdateImageRequest
{
    public Guid? Id { get; set; }
    
    [Required]
    public required string Uri { get; set; }
}
