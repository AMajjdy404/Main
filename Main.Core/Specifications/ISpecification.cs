using System.Linq.Expressions;

namespace Main.Core.Specifications
{
    public interface ISpecification<T>
    {
        Expression<Func<T, bool>>? Criteria { get; }

        List<Expression<Func<T, object>>> Includes { get; }

        List<string> IncludeStrings { get; }

        Expression<Func<T, object>>? OrderBy { get; }

        Expression<Func<T, object>>? OrderByDescending { get; }

        List<Expression<Func<T, object>>> ThenByExpressions { get; }

        List<Expression<Func<T, object>>> ThenByDescendingExpressions { get; }

        int? Take { get; }

        int? Skip { get; }

        bool IsPagingEnabled { get; }
    }
}
