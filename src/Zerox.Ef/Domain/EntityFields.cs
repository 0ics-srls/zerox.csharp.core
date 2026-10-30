using System.Reflection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// What a client may change of an entity it sends whole (a PUT): every public field with a setter,
/// except the key, the navigations and the reserved fields. A reserved field never reaches the client,
/// so it comes back empty: copied, it would wipe the stored value. A navigation is not the client's to
/// rewrite through its parent.
/// </summary>
public static class EntityFields
{
    /// <summary>
    /// Copies the editable fields of source onto target (usually the stored, tracked row): only what
    /// really changed is then written.
    /// </summary>
    public static void CopyEditable<T>(T source, T target) where T : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        foreach (var property in Selection<T>.Properties)
            property.SetValue(target, property.GetValue(source));
    }

    /// <summary>
    /// The names of the fields a client may write on an entity it sends: every public field with a setter,
    /// except the key, the navigations and the reserved fields — what IRepository.Update marks modified.
    /// </summary>
    public static IReadOnlyCollection<string> Editable<T>() where T : class, IEntity => Selection<T>.Names;

    private static class Selection<T>
    {
        internal static readonly PropertyInfo[] Properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            .Where(p => p.Name != nameof(IEntity.Id))
            .Where(p => !EntityPaths.IsNavigation(p.PropertyType) && !EntityPaths.IsReserved(p))
            .ToArray();

        internal static readonly string[] Names = Properties.Select(p => p.Name).ToArray();
    }
}
