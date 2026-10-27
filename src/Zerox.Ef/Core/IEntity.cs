namespace Zerox.Ef.Core;

/// <summary>
/// An entity with its own int key: what the repositories and the base controller work on.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// The key, given by the database when the row is added (0 before).
    /// </summary>
    int Id { get; set; }
}
