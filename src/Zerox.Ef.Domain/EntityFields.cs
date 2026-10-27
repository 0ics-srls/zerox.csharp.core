using System.Reflection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

// What a client may change of an entity it sends whole (a PUT): every public field with a setter,
// except the key, the navigations and the reserved fields. A reserved field never reaches the client,
// so it comes back empty: copied, it would wipe the stored value. A navigation is not the client's to
// rewrite through its parent.
public static class EntityFields
{
    // Copies the editable fields of source onto target (usually the stored, tracked row): only what
    // really changed is then written.
    public static void CopyEditable<T>(T source, T target) where T : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        foreach (var property in Editable<T>.Properties)
            property.SetValue(target, property.GetValue(source));
    }

    private static class Editable<T>
    {
        internal static readonly PropertyInfo[] Properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            .Where(p => p.Name != nameof(IEntity.Id))
            .Where(p => !EntityPaths.IsNavigation(p.PropertyType) && !EntityPaths.IsReserved(p))
            .ToArray();
    }
}
