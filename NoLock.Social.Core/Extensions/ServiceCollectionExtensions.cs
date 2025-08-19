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
using NoLock.Social.Core.ImageProcessing.Interfaces;
using NoLock.Social.Core.ImageProcessing.Services;
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
            // Image enhancement services for OCR optimization
            services.AddScoped<IImageEnhancementService, ImageEnhancementService>();
            
            return services;
        }
        
        public static IServiceCollection AddOCRServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure OCR service options from configuration
            services.Configure<OCRServiceOptions>(configuration.GetSection(OCRServiceOptions.SectionName));
            
            // TODO: Add OCRServiceOptionsValidator when implemented
            // services.AddSingleton<IValidateOptions<OCRServiceOptions>, OCRServiceOptionsValidator>();
            
            // Register base OCR service
            services.AddScoped<OCRService>();
            
            // Register retry infrastructure
            services.AddScoped<IFailureClassifier, OCRFailureClassifier>();
            services.AddScoped<IRetryPolicy>(provider =>
            {
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ExponentialBackoffRetryPolicy>>();
                var classifier = provider.GetRequiredService<IFailureClassifier>();
                var options = provider.GetRequiredService<IOptions<OCRServiceOptions>>().Value;
                
                // Use configuration if available, otherwise defaults
                return new ExponentialBackoffRetryPolicy(
                    logger,
                    classifier,
                    maxAttempts: 3,
                    initialDelayMs: 1000,
                    maxDelayMs: 30000,
                    backoffMultiplier: 2.0);
            });
            
            services.AddScoped<RetryableOperationFactory>();
            services.AddScoped<IFailedRequestStore, IndexedDbFailedRequestStore>();
            
            // Register OCR result cache
            services.AddScoped<IOCRResultCache>(provider =>
            {
                var storage = provider.GetRequiredService<IContentAddressableStorage>();
                var hashAlgorithm = provider.GetRequiredService<IHashAlgorithm>();
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OCRResultCache>>();
                var options = provider.GetRequiredService<IOptions<OCRServiceOptions>>().Value;
                
                // Use configured cache expiration or default to 60 minutes
                var expirationMinutes = options.CacheExpirationMinutes ?? 60;
                
                return new OCRResultCache(storage, hashAlgorithm, logger, expirationMinutes);
            });
            
            // Register OCR service with retry and cache decorators
            services.AddScoped<IOCRService>(provider =>
            {
                var innerService = provider.GetRequiredService<OCRService>();
                var retryPolicy = provider.GetRequiredService<IRetryPolicy>();
                var operationFactory = provider.GetRequiredService<RetryableOperationFactory>();
                var retryLogger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OCRServiceWithRetry>>();
                var connectivityService = provider.GetService<IConnectivityService>();
                var offlineQueueService = provider.GetService<IOfflineQueueService>();
                
                // Create retry decorator
                var serviceWithRetry = new OCRServiceWithRetry(
                    innerService,
                    retryPolicy,
                    operationFactory,
                    retryLogger,
                    connectivityService,
                    offlineQueueService);
                
                // Wrap with cache decorator if caching is enabled
                var options = provider.GetRequiredService<IOptions<OCRServiceOptions>>().Value;
                if (options.EnableCaching ?? true) // Default to enabled
                {
                    var cache = provider.GetRequiredService<IOCRResultCache>();
                    var cacheLogger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OCRServiceWithCache>>();
                    
                    return new OCRServiceWithCache(
                        serviceWithRetry,
                        cache,
                        cacheLogger,
                        options.CacheOnlyCompleteResults ?? true,
                        options.CacheExpirationMinutes ?? 60);
                }
                
                return serviceWithRetry;
            });
            
            // Register retry queue processor (singleton for background processing)
            services.AddSingleton<OCRRetryQueueProcessor>();
            
            // Register generic polling service for OCR status responses
            services.AddScoped<IPollingService<OCRStatusResponse>, PollingService<OCRStatusResponse>>();
            
            // Register Wake Lock Service for preventing device sleep during processing
            services.AddScoped<IWakeLockService, WakeLockService>();
            
            // Register OCR-specific polling service
            services.AddScoped<IOCRPollingService, OCRPollingService>();
            
            // Register document type detector with default confidence threshold
            services.AddScoped<IDocumentTypeDetector>(provider =>
            {
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DocumentTypeDetector>>();
                var options = provider.GetService<IOptions<OCRServiceOptions>>()?.Value;
                var confidenceThreshold = options?.MinimumConfidenceThreshold ?? 0.7;
                return new DocumentTypeDetector(logger, confidenceThreshold);
            });
            
            // Register document processor registry with detector support
            services.AddScoped<IDocumentProcessorRegistry>(provider =>
            {
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DocumentProcessorRegistry>>();
                var detector = provider.GetService<IDocumentTypeDetector>();
                var registry = new DocumentProcessorRegistry(logger, detector);
                
                // Register processors with metadata
                var receiptProcessor = provider.GetRequiredService<ReceiptProcessor>();
                registry.RegisterProcessor(
                    receiptProcessor,
                    "Receipt Processor",
                    "Processes receipt documents to extract structured data including merchant, total, date, and line items",
                    new Version(1, 0, 0),
                    priority: 100,
                    capabilities: new[] { "line-item-extraction", "total-calculation", "merchant-detection" },
                    supportedExtensions: new[] { ".jpg", ".jpeg", ".png", ".pdf" }
                );
                
                var checkProcessor = provider.GetRequiredService<CheckProcessor>();
                registry.RegisterProcessor(
                    checkProcessor,
                    "Check Processor",
                    "Processes bank checks to extract routing numbers, account numbers, check numbers, and amounts",
                    new Version(1, 0, 0),
                    priority: 90,
                    capabilities: new[] { "micr-extraction", "amount-detection", "signature-detection" },
                    supportedExtensions: new[] { ".jpg", ".jpeg", ".png", ".pdf" }
                );
                
                return registry;
            });
            
            // Register document processors for DI
            services.AddScoped<IDocumentProcessor, ReceiptProcessor>();
            services.AddScoped<ReceiptProcessor>();
            services.AddScoped<IDocumentProcessor, CheckProcessor>();
            services.AddScoped<CheckProcessor>();
            
            // TODO: Configure named HttpClient for OCR service when HTTP client extensions are available
            // For now, register HttpClient directly
            services.AddScoped<HttpClient>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<OCRServiceOptions>>().Value;
                var client = new HttpClient();
                
                // Set base address if configured
                if (!string.IsNullOrEmpty(options.BaseUrl))
                {
                    client.BaseAddress = new Uri(options.BaseUrl);
                }
                
                // Set timeout from configuration
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                
                // Add default headers if needed
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "NoLock.Social.OCR/1.0");
                
                return client;
            });
            
            return services;
        }
    }
}