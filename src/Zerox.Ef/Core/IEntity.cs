namespace Zerox.Ef.Core;

// An entity with its own int key: what the repositories and the base controller work on.
public interface IEntity
{
    int Id { get; set; }
}
