using Main.Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Main.Infrastructure.ReposAndSpecs
{
    public static class SpecificationEvaluator<T> where T : class
    {
        public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> spec)
        {
            var query = inputQuery;

            if (spec.Criteria is not null)
                query = query.Where(spec.Criteria);

            query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

            query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

            IOrderedQueryable<T>? orderedQuery = null;

            if (spec.OrderBy is not null)
            {
                orderedQuery = query.OrderBy(spec.OrderBy);

                foreach (var thenBy in spec.ThenByExpressions)
                    orderedQuery = orderedQuery.ThenBy(thenBy);

                foreach (var thenByDesc in spec.ThenByDescendingExpressions)
                    orderedQuery = orderedQuery.ThenByDescending(thenByDesc);
            }
            else if (spec.OrderByDescending is not null)
            {
                orderedQuery = query.OrderByDescending(spec.OrderByDescending);

                foreach (var thenBy in spec.ThenByExpressions)
                    orderedQuery = orderedQuery.ThenBy(thenBy);

                foreach (var thenByDesc in spec.ThenByDescendingExpressions)
                    orderedQuery = orderedQuery.ThenByDescending(thenByDesc);
            }

            query = orderedQuery ?? query;

            if (spec.IsPagingEnabled)
                query = query.Skip(spec.Skip ?? 0).Take(spec.Take ?? 0);

            return query;
        }
    }
}
