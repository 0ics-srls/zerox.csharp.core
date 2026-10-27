using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db;

/// <summary>
/// The registration of a context with the library.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The context with the session rules, and the ports on it. The unit of work and the massive
    /// handler are not registered: a repository hands them out. The end of the scope closes a unit of work
    /// or a massive operation left open and throws InvalidOperationException: an await using was forgotten.
    /// </summary>
    public static IServiceCollection AddZeroxDb<TContext>(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext, IDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddDbContext<TContext>(options =>
        {
            configure(options);
            options.UseZeroxSession();
        });
        services.AddScoped(sp => new SessionScopeCheck(sp.GetRequiredService<TContext>()));
        services.AddScoped<IDbContext>(sp =>
        {
            sp.GetRequiredService<SessionScopeCheck>();
            return sp.GetRequiredService<TContext>();
        });
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        return services;
    }
}
