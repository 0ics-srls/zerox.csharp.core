namespace Zerox.Ef.Core;

/// <summary>
/// The repository of a type decided at run time, on the same context as every other repository of
/// the scope. For code that cannot take IRepository&lt;T&gt; in its constructor (a non-generic class with a
/// generic method); whoever can inject IRepository&lt;T&gt; does.
/// </summary>
public interface IRepositoryFactory
{
    /// <summary>
    /// The repository of <typeparamref name="T"/> on the context of the scope.
    /// </summary>
    IRepository<T> For<T>() where T : class, IEntity;
}
