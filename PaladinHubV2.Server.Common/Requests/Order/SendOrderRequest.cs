using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Common.Requests.Order;

public class SendOrderRequest
{
    [Required]
    public required string Names { get; set; }
    
    [Required]
    public required string PostalCode { get; set; }
    
    [Required]
    public required string Country { get; set; }
    
    [Required]
    public required string City { get; set; }
    
    [Required]
    public required string Address { get; set; }
    
    [Required]
    public required string Phone { get; set; }

    public string? PaymentMethod { get; set; }

    public string? DeliveryMethod { get; set; }

    public bool ConsentAccepted { get; set; }
}
