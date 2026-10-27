using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Zerox.Db;

public static class DbContextOptionsBuilderExtensions
{
    // Adds the session rules to the context. AddZeroxDb calls it; a context built by hand calls it too.
    public static DbContextOptionsBuilder UseZeroxSession(this DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddInterceptors(SessionInterceptor.Instance);
    }

    // The typed builder stays typed, like EF's own Use* methods.
    public static DbContextOptionsBuilder<TContext> UseZeroxSession<TContext>(this DbContextOptionsBuilder<TContext> builder)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)UseZeroxSession((DbContextOptionsBuilder)builder);

    internal static bool HasZeroxSession(IDbContext context) =>
        context is IInfrastructure<IServiceProvider> infrastructure
        && infrastructure.Instance.GetService<IDbContextOptions>()?
            .FindExtension<CoreOptionsExtension>()?.Interceptors?.Contains(SessionInterceptor.Instance) == true;
}
