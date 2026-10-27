using System.Linq.Expressions;

namespace Zerox.Ef.Core;

/// <summary>
/// The fields a massive update sets, declared without the writer's own types: the contract stays free
/// of EF. One method because there is one case, a constant value:
///   massive.UpdateWhereAsync(r =&gt; r.OwnerId == id, fields =&gt; fields.Set(r =&gt; r.Status, Status.Closed));
/// </summary>
public interface IFieldsToSet<T>
{
    /// <summary>
    /// Returns itself, to chain several fields in the same update.
    /// </summary>
    IFieldsToSet<T> Set<TField>(Expression<Func<T, TField>> field, TField value);
}
