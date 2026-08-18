using System.ComponentModel.DataAnnotations;
using PaladinHubV2.Core.Enums;

namespace PaladinHubV2.Common.Requests.Order;

public class ChangeOrderStatusRequest
{
    [Required]
    public required Guid OrderId { get; set; }
    
    [Required]
    public required OrderStatus OrderStatus { get; set; }
}
