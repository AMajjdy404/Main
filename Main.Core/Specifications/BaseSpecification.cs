using System.Linq.Expressions;

namespace Main.Core.Specifications
{
    public abstract class BaseSpecification<T> : ISpecification<T>
    {
        public Expression<Func<T, bool>>? Criteria { get; protected set; }

        public List<Expression<Func<T, object>>> Includes { get; } = new();

        public List<string> IncludeStrings { get; } = new();

        public Expression<Func<T, object>>? OrderBy { get; protected set; }

        public Expression<Func<T, object>>? OrderByDescending { get; protected set; }

        public List<Expression<Func<T, object>>> ThenByExpressions { get; } = new();

        public List<Expression<Func<T, object>>> ThenByDescendingExpressions { get; } = new();

        public int? Take { get; protected set; }

        public int? Skip { get; protected set; }

        public bool IsPagingEnabled { get; protected set; }

        protected BaseSpecification()
        {
        }

        protected BaseSpecification(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }

        protected void AddInclude(Expression<Func<T, object>> includeExpression)
            => Includes.Add(includeExpression);

        protected void AddInclude(string includeString)
            => IncludeStrings.Add(includeString);

        protected void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
            IsPagingEnabled = true;
        }

        protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
            => OrderBy = orderByExpression;

        protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
            => OrderByDescending = orderByDescExpression;

        protected void ApplyThenBy(Expression<Func<T, object>> thenByExpression)
            => ThenByExpressions.Add(thenByExpression);

        protected void ApplyThenByDescending(Expression<Func<T, object>> thenByDescExpression)
            => ThenByDescendingExpressions.Add(thenByDescExpression);
    }
}
