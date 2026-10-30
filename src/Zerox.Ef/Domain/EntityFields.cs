using System.Reflection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// What a client may change of an entity it sends whole (a PUT): every public field with a setter,
/// except the key, the navigations and the reserved fields. A reserved field never reaches the client,
/// so it comes back empty: written, it would wipe the stored value. A navigation is not the client's to
/// rewrite through its parent.
/// </summary>
public static class EntityFields
{
    /// <summary>
    /// The names of the fields a client may write on an entity it sends: every public field with a setter,
    /// except the key, the navigations and the reserved fields — what IRepository.Update marks modified.
    /// </summary>
    public static IReadOnlyCollection<string> Editable<T>() where T : class, IEntity => Selection<T>.Names;

    private static class Selection<T>
    {
        internal static readonly string[] Names = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            .Where(p => p.Name != nameof(IEntity.Id))
            .Where(p => !EntityPaths.IsNavigation(p.PropertyType) && !EntityPaths.IsReserved(p))
            .Select(p => p.Name)
            .ToArray();
    }
}
