using Microsoft.Extensions.DependencyInjection;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;

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

        public static IServiceCollection AddCryptographicServices(this IServiceCollection services)
        {
            // Browser compatibility
            services.AddScoped<IBrowserCompatibilityService, BrowserCompatibilityService>();
            
            // JavaScript interop for crypto operations
            services.AddScoped<ICryptoJSInteropService, CryptoJSInteropService>();
            
            // Secure memory management
            services.AddSingleton<ISecureMemoryManager, SecureMemoryManager>();
            
            // Session state management
            services.AddScoped<ISessionStateService, SessionStateService>();
            
            // Key derivation and generation
            services.AddScoped<IKeyDerivationService, KeyDerivationService>();
            
            return services;
        }
    }
}