using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zerox.Ef.Core;

namespace Zerox.Ef.Domain;

/// <summary>
/// The registration of the entity services, after AddZeroxDb: every entity gets an
/// <see cref="IEntityService{T}"/>, and a derived service replaces it for its entity.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// An <see cref="IEntityService{T}"/> for every entity, scoped like the repository it uses:
    /// <see cref="EntityService{T}"/>, unless a derived one was registered for that entity (before or after).
    /// </summary>
    public static IServiceCollection AddZeroxDomain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IEntityService<>), typeof(EntityService<>)));
        return services;
    }

    /// <summary>
    /// A derived service of <typeparamref name="T"/>, one instance per scope behind its own contract and
    /// <see cref="IEntityService{T}"/>: the controller of the entity, which asks the base contract, gets it too.
    /// </summary>
    /// <typeparam name="T">The entity.</typeparam>
    /// <typeparam name="TContract">The service's own contract; <see cref="IEntityService{T}"/> when it has none.</typeparam>
    /// <typeparam name="TService">The derived service.</typeparam>
    public static IServiceCollection AddEntityService<T, TContract, TService>(this IServiceCollection services)
        where T : class, IEntity
        where TContract : class, IEntityService<T>
        where TService : EntityService<T>, TContract
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<TService>();
        services.AddScoped<TContract>(sp => sp.GetRequiredService<TService>());
        if (typeof(TContract) != typeof(IEntityService<T>))
            services.AddScoped<IEntityService<T>>(sp => sp.GetRequiredService<TService>());
        return services;
    }
}
