namespace Zerox.Core.Data;

// The repository of a type decided at run time, on the same context as every other repository of
// the scope. For code that cannot take IRepository<T> in its constructor (a non-generic class with a
// generic method); whoever can inject IRepository<T> does.
public interface IRepositoryFactory
{
    IRepository<T> For<T>() where T : class, IEntity;
}
