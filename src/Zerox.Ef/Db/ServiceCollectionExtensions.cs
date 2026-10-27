using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Ef.Core;

namespace Zerox.Ef.Db;

public static class ServiceCollectionExtensions
{
    // The context with the session rules, and the ports on it. The unit of work and the massive
    // handler are not registered: a repository hands them out. The end of the scope checks that none of
    // them was left open (SessionScopeCheck, created with the context and disposed before it).
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
