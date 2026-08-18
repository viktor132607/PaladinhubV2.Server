using System.Linq.Expressions;

namespace PaladinHubV2.Data.PaginationAndFiltering;

public class Filter<TEntity>
    where TEntity : class
{
    public Filter()
    {
        Includes = Enumerable.Empty<Expression<Func<TEntity, object>>>();
        IncludesAsPropertyPath = Enumerable.Empty<string>();
        Predicate = (e) => true;
    }

    public Filter<TEntity>? Empty { get; private set; }

    public IEnumerable<Expression<Func<TEntity, object>>> Includes { get; set; }

    public IEnumerable<string> IncludesAsPropertyPath { get; set; }

    public Expression<Func<TEntity, bool>> Predicate { get; set; }

    public int? PageNumber { get; set; }

    public int? PageSize { get; set; }

    public string? SortBy { get; set; }

    public bool? SortDescending { get; set; }
}

