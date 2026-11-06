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
        services.AddScoped<SessionScopeCheck>();
        services.AddScoped<IDbContext>(sp =>
        {
            var context = sp.GetRequiredService<TContext>();
            sp.GetRequiredService<SessionScopeCheck>().Watch(context);   // created after the context: disposed before it
            return context;
        });
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        return services;
    }
}
