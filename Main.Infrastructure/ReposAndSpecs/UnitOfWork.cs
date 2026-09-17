using Main.Core.Interfaces;
using Main.Infrastructure.Data;

namespace Main.Infrastructure.ReposAndSpecs
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        private Dictionary<string, object>? _repositories;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            _repositories ??= new Dictionary<string, object>();

            var typeName = typeof(TEntity).Name;

            if (!_repositories.TryGetValue(typeName, out var repository))
            {
                var repositoryInstance = new GenericRepository<TEntity>(_context);
                _repositories[typeName] = repositoryInstance;
                repository = repositoryInstance;
            }

            return (IGenericRepository<TEntity>)repository;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await action();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
