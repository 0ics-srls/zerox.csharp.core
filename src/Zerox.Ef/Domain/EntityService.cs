using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// The operations of an entity over its repository, for the API and for an in-process UI alike: the search,
/// the distinct values, the CRUD at the version the caller read. Every outcome is an <see cref="OpResult{T}"/>,
/// never an exception. Derive it for an entity's own operations and run each through
/// <see cref="RunAsync{TResult}"/>. Registered for every entity by AddZeroxDomain; a derived one by
/// AddEntityService.
/// </summary>
/// <typeparam name="T">The entity.</typeparam>
/// <param name="repository">The repository of the entity.</param>
public class EntityService<T>(IRepository<T> repository) : IEntityService<T> where T : class, IEntity
{
    private const string ChangedSince = "The row was changed by another user.";

    /// <summary>
    /// The repository of the entity, for a derived service's operations.
    /// </summary>
    protected IRepository<T> Repository { get; } = repository;

    /// <summary>
    /// The most rows one load reads; a service with larger pages overrides it.
    /// </summary>
    protected virtual int MaxTake => Search.DefaultMaxTake;

    /// <summary>
    /// The rows the load works on; a service with its own view of the rows overrides it.
    /// </summary>
    protected virtual IQueryable<T> SearchSource() => Repository.Query();

    /// <inheritdoc />
    public virtual Task<OpResult<Loaded>> LoadAsync(LoadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return RunAsync(c => Search.LoadAsync(SearchSource(), options, MaxTake, c), ct);
    }

    /// <inheritdoc />
    public virtual Task<OpResult<IReadOnlyList<TextValue>>> ValuesAsync(string column, LoadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(options);
        return RunAsync(c => DistinctTextValues.IsTextColumn<T>(column)
            ? DistinctTextValues.ValuesAsync(SearchSource(), column, options.Filter, MaxTake, c)
            : Task.FromResult(OpResult.Invalid<IReadOnlyList<TextValue>>($"'{column}' is not a readable text column of {typeof(T).Name}.")), ct);
    }

    /// <inheritdoc />
    public virtual Task<OpResult<T>> GetAsync(int id, CancellationToken ct = default) =>
        RunAsync(async c => await Repository.GetByIdAsync(id, c).ConfigureAwait(false) is { } entity
            ? OpResult.Ok(entity)
            : OpResult.NotFound<T>(), ct);

    /// <inheritdoc />
    public virtual Task<OpResult<T>> CreateAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return RunAsync(async c =>
        {
            if (entity.Id != 0)
                return OpResult.Invalid<T>("The Id is given by the database: leave it out of a creation.");
            await Repository.AddAsync(entity, c).ConfigureAwait(false);
            await Repository.SaveChangesAsync(c).ConfigureAwait(false);
            return OpResult.Created(entity);
        }, ct);
    }

    /// <inheritdoc />
    public virtual Task<OpResult<T>> UpdateAsync(T entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return RunAsync(async c =>
        {
            Repository.Update(entity, EntityFields.Editable<T>());
            if (!await SaveUnlessMissingAsync(entity.Id, c).ConfigureAwait(false))
                return OpResult.NotFound<T>();
            return OpResult.Ok(await Repository.Query().SingleAsync(e => e.Id == entity.Id, c).ConfigureAwait(false));
        }, ct);
    }

    /// <inheritdoc />
    public virtual Task<OpResult<T>> DeleteAsync(int id, ulong? version, CancellationToken ct = default) =>
        RunAsync(async c =>
        {
            if (Repository.HasRowVersion && version is null)
                return OpResult.Invalid<T>($"{typeof(T).Name} has an optimistic lock: give the Version you read, as ?version=N.");
            if (!Repository.TryDeleteById(id, version))
                return OpResult.Invalid<T>($"{version} is not a version of {typeof(T).Name}: give the Version you read.");
            return await SaveUnlessMissingAsync(id, c).ConfigureAwait(false) ? OpResult.Deleted<T>() : OpResult.NotFound<T>();
        }, ct);

    /// <summary>
    /// Runs an operation and turns what it throws into its status: <see cref="InvalidRequestException"/> →
    /// Invalid, <see cref="StaleVersionException"/> or an optimistic-lock conflict → Conflict, anything else
    /// (cancellation included) → Faulted with the exception. The one place where an exception becomes an outcome:
    /// every operation of the base and of a derived service runs through it.
    /// </summary>
    protected async Task<OpResult<TResult>> RunAsync<TResult>(Func<CancellationToken, Task<OpResult<TResult>>> operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        catch (InvalidRequestException e)
        {
            return OpResult.Invalid<TResult>(e.Message);
        }
        catch (StaleVersionException e)
        {
            return OpResult.Conflict<TResult>(e.Message);
        }
        catch (DbUpdateConcurrencyException e) when (OptimisticLockConflict.Is(e))
        {
            return OpResult.Conflict<TResult>(ChangedSince);
        }
        catch (Exception e)
        {
            return OpResult.Faulted<TResult>(e);
        }
    }

    /// <summary>
    /// Saves a caller's write. An UPDATE or DELETE that hit no row fails the same way whether the row is missing
    /// or changed since it was read; only then one read tells them apart: false when the row is missing, the
    /// conflict goes on (<see cref="RunAsync{TResult}"/> makes it Conflict) when it exists.
    /// </summary>
    protected async Task<bool> SaveUnlessMissingAsync(int id, CancellationToken ct)
    {
        try
        {
            await Repository.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (await Repository.Query().AnyAsync(e => e.Id == id, ct).ConfigureAwait(false))
                throw;
            return false;
        }
    }
}
