using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NoLock.Social.Core.Storage;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace Nolock.Social.Storage.IndexedDb;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndexedDbContentAddressableStorage<T>(this IServiceCollection services)
    {
        services.AddScoped<IndexedDbCasDatabase>();
        services.AddScoped<ISerializer<T>, JsonSerializer<T>>();
        services.AddScoped<IndexedDbContentAddressableStorage<T>>();
        services.AddScoped<IContentAddressableStorage<T>, IndexedDbContentAddressableStorage<T>>();
        
        return services;
    }
}