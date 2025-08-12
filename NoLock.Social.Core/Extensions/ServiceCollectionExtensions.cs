using Microsoft.Extensions.DependencyInjection;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Hashing;

namespace NoLock.Social.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddContentAddressableStorage(this IServiceCollection services)
        {
            services.AddScoped<IHashAlgorithm, SHA256HashAlgorithm>();
            services.AddScoped<IIndexedDBManagerWrapper, IndexedDBManagerWrapper>();
            services.AddScoped<IContentAddressableStorage, IndexedDBContentAddressableStorage>();
            return services;
        }
    }
}