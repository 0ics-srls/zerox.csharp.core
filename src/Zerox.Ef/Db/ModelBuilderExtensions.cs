using Microsoft.EntityFrameworkCore;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db;

/// <summary>
/// The library's part of a context's model.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Marks Version as concurrency token on every entity implementing IHasOptimisticLock. Call it at
    /// the END of OnModelCreating, when the entities are in the model.
    /// Never IsRowVersion(): on Npgsql it remaps the property to the system column xmin and the
    /// migration fails. A column of our own is what is needed.
    /// </summary>
    public static ModelBuilder ApplyOptimisticLock(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (var type in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IHasOptimisticLock).IsAssignableFrom(t.ClrType)))
            modelBuilder.Entity(type.ClrType).Property(nameof(IHasOptimisticLock.Version)).IsConcurrencyToken();
        return modelBuilder;
    }
}
