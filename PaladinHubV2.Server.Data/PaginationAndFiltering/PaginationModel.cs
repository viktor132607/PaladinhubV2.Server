namespace PaladinHubV2.Data.PaginationAndFiltering;

public class PaginationModel
{
    public int? PageNumber { get; set; }

    public int? PageSize { get; set; }

    public string? SortBy { get; set; }

    public bool? SortDescending { get; set; }
}
