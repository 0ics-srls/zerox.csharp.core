using System.Linq.Expressions;

namespace Zerox.Ef.Core;

// The fields a massive update sets, declared without the writer's own types: the contract stays free
// of EF. One method because there is one case, a constant value:
//   massive.UpdateWhereAsync(r => r.OwnerId == id, fields => fields.Set(r => r.Status, Status.Closed));
public interface IFieldsToSet<T>
{
    // Returns itself, to chain several fields in the same update.
    IFieldsToSet<T> Set<TField>(Expression<Func<T, TField>> field, TField value);
}
