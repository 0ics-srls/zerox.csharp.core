using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zerox.Core.Data;

namespace Zerox.Db;

public static class ServiceCollectionExtensions
{
    // The context with the session rules, and the ports on it. The unit of work and the massive
    // handler are not registered: a repository hands them out.
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
        services.AddScoped<IDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        return services;
    }
}
