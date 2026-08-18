using System.Linq.Expressions;

namespace PaladinHubV2.Data.PaginationAndFiltering;

public static class ExpressionExtension<TEntity>
{
    public static Expression<Func<TEntity, bool>> AndAlso(
        Expression<Func<TEntity, bool>> first,
        Expression<Func<TEntity, bool>> second)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "s");

        Expression<Func<TEntity, bool>> combined = Expression.Lambda<Func<TEntity, bool>>(
            Expression.AndAlso(
                Expression.Invoke(first, parameter),
                Expression.Invoke(second, parameter)),
            parameter);

        return combined;
    }

    public static Expression<Func<TEntity, bool>> OrElse(
        Expression<Func<TEntity, bool>> first,
        Expression<Func<TEntity, bool>> second)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "s");

        Expression<Func<TEntity, bool>> combined = Expression.Lambda<Func<TEntity, bool>>(
            Expression.AndAlso(
                Expression.Invoke(first, parameter),
                Expression.Invoke(second, parameter)),
            parameter);

        return combined;
    }
}
