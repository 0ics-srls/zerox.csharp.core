using Zerox.Core.Data;

namespace Zerox.Db;

// Repositories on the SAME context of the scope: what one does, the other sees.
internal sealed class RepositoryFactory(IDbContext db) : IRepositoryFactory
{
    public IRepository<T> For<T>() where T : class, IEntity => new Repository<T>(db);
}
