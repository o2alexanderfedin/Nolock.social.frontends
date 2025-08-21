using Microsoft.Extensions.DependencyInjection;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Storage.Interfaces;
using NoLock.Social.Core.Storage.Services;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Services;
using NoLock.Social.Core.Security;
using NoLock.Social.Core.Performance;
using NoLock.Social.Core.Accessibility.Interfaces;
using NoLock.Social.Core.Accessibility.Services;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Services;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Configuration;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Processors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

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

        public static IServiceCollection AddCryptographicServices(this IServiceCollection services, bool useReactive = false)
        {
            // Browser compatibility
            services.AddScoped<IBrowserCompatibilityService, BrowserCompatibilityService>();
            
            // Web Crypto API interop
            services.AddScoped<IWebCryptoService, WebCryptoService>();
            
            // Secure memory management
            services.AddSingleton<ISecureMemoryManager, SecureMemoryManager>();
            
            // Session state management - use reactive version if specified
            if (useReactive)
            {
                services.AddScoped<ReactiveSessionStateService>();
                services.AddScoped<ISessionStateService>(provider => provider.GetRequiredService<ReactiveSessionStateService>());
                services.AddScoped<IReactiveSessionStateService>(provider => provider.GetRequiredService<ReactiveSessionStateService>());
            }
            else
            {
                services.AddScoped<ISessionStateService, SessionStateService>();
            }
            
            // Key derivation and generation
            services.AddScoped<IKeyDerivationService, KeyDerivationService>();
            
            // Signing and verification services
            services.AddScoped<ISigningService, SigningService>();
            services.AddScoped<IVerificationService, VerificationService>();
            
            // Storage adapter service
            services.AddScoped<IStorageAdapterService, StorageAdapterService>();
            
            // Error handling service
            services.AddScoped<ICryptoErrorHandlingService, CryptoErrorHandlingService>();
            
            return services;
        }
        
        public static IServiceCollection AddSecurityServices(this IServiceCollection services)
        {
            // Security configuration and services
            services.AddScoped<ISecurityService, SecurityService>();
            
            return services;
        }
        
        public static IServiceCollection AddPerformanceMonitoring(this IServiceCollection services)
        {
            // Performance monitoring service
            services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
            
            return services;
        }
        
        public static IServiceCollection AddLoginServices(this IServiceCollection services)
        {
            // New login layer services that wrap existing identity unlock
            services.AddScoped<IUserTrackingService, UserTrackingService>();
            services.AddScoped<IRememberMeService, RememberMeService>();
            // Use the secure session persistence that doesn't store private keys
            services.AddScoped<ISessionPersistenceService, SecureSessionPersistenceService>();
            services.AddScoped<ILoginAdapterService, LoginAdapterService>();
            
            return services;
        }
        
        public static IServiceCollection AddOfflineStorageServices(this IServiceCollection services)
        {
            // Offline storage and queue services
            services.AddScoped<IOfflineStorageService, IndexedDbStorageService>();
            services.AddScoped<IOfflineQueueService, OfflineQueueService>();
            services.AddScoped<IConnectivityService, ConnectivityService>();
            
            return services;
        }
        
        public static IServiceCollection AddAccessibilityServices(this IServiceCollection services)
        {
            // Voice command service for accessibility
            services.AddScoped<IVoiceCommandService, VoiceCommandService>();
            
            // Focus management service for keyboard navigation
            services.AddScoped<IFocusManagementService, FocusManagementService>();
            
            // Live region announcement service for screen readers
            services.AddScoped<IAnnouncementService, AnnouncementService>();
            
            return services;
        }
        
        public static IServiceCollection AddCameraServices(this IServiceCollection services)
        {
            // Camera capture and document processing services
            services.AddScoped<ICameraService, CameraService>();
            
            return services;
        }
        
        public static IServiceCollection AddImageProcessingServices(this IServiceCollection services)
        {
            // No image processing services needed anymore - we only do OCR via external API
            return services;
        }
        
    }
}