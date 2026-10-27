using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db;

// The handle of the exclusive mode: forwards to the session, which owns the state and the transaction.
// It must be an object of its own: something has to roll back when the mode is closed without commit.
internal sealed class MassiveOpHandler<T>(DbSession session, IDbContext db) : IMassiveOpHandler<T> where T : class
{
    public Task<int> UpdateWhereAsync(Expression<Func<T, bool>> where, Action<IFieldsToSet<T>> fields, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var toSet = new FieldsToSet();
        fields(toSet);
        return db.Set<T>().Where(where).ExecuteUpdateAsync(toSet.Apply, ct);
    }

    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> where, CancellationToken ct = default) =>
        db.Set<T>().Where(where).ExecuteDeleteAsync(ct);

    public Task CommitAsync(CancellationToken ct = default) => session.CommitMassiveAsync(this, ct);

    public ValueTask DisposeAsync() => session.CloseMassiveAsync(this);

    // The translator: collects the fields declared with the contract and says them to EF when EF asks
    // (its builder exists only inside ExecuteUpdateAsync), so the contract stays free of EF types.
    private sealed class FieldsToSet : IFieldsToSet<T>
    {
        private readonly List<Action<UpdateSettersBuilder<T>>> _steps = [];

        public IFieldsToSet<T> Set<TField>(Expression<Func<T, TField>> field, TField value)
        {
            _steps.Add(builder => builder.SetProperty(field, value));
            return this;
        }

        internal void Apply(UpdateSettersBuilder<T> builder)
        {
            foreach (var step in _steps)
                step(builder);
        }
    }
}
