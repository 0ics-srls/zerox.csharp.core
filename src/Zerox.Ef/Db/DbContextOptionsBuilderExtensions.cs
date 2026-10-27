using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Zerox.Ef.Db;

/// <summary>
/// The session rules on the options of a context built by hand.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds the session rules to the context. AddZeroxDb calls it; a context built by hand calls it too.
    /// </summary>
    public static DbContextOptionsBuilder UseZeroxSession(this DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddInterceptors(SessionInterceptor.Instance);
    }

    /// <summary>
    /// The typed builder stays typed, like EF's own Use* methods.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseZeroxSession<TContext>(this DbContextOptionsBuilder<TContext> builder)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)UseZeroxSession((DbContextOptionsBuilder)builder);

    internal static bool HasZeroxSession(IDbContext context) =>
        context is IInfrastructure<IServiceProvider> infrastructure
        && infrastructure.Instance.GetService<IDbContextOptions>()?
            .FindExtension<CoreOptionsExtension>()?.Interceptors?.Contains(SessionInterceptor.Instance) == true;
}
