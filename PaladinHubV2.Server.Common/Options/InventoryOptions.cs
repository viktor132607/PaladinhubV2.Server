namespace PaladinHubV2.Common.Options;

public class InventoryOptions
{
    public const string SectionName = "Inventory";

    public int LowStockThreshold { get; set; } = 10;
}
