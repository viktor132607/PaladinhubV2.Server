using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Image;

public class CreateImageRequest
{
    [Required]
    public required string Uri { get; set; }
}
