using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Configuration;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Generated;

namespace NoLock.Social.Core.Extensions
{
    /// <summary>
    /// OCR service registration extensions
    /// </summary>
    public static class OCRServiceExtensions
    {
        /// <summary>
        /// Adds OCR services that directly call the Mistral OCR API
        /// </summary>
        public static IServiceCollection AddOCRServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure OCR service options from configuration
            services.Configure<OCRServiceOptions>(configuration.GetSection(OCRServiceOptions.SectionName));
            
            // Configure HttpClient for MistralOCRClient without auto-registration
            services.AddHttpClient("MistralOCRClient", client =>
            {
                // Set base URL for Mistral OCR API
                client.BaseAddress = new Uri("https://nolock-ocr-services-qbhx5.ondigitalocean.app");
                
                // Set default timeout (30 seconds)
                client.Timeout = TimeSpan.FromSeconds(30);
                
                // Add default headers if needed
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "NoLock-OCR-Client/1.0");
            });
            
            // Register MistralOCRClient as Scoped with configured HttpClient
            services.AddScoped<MistralOCRClient>(provider =>
            {
                var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("MistralOCRClient");
                return new MistralOCRClient("https://nolock-ocr-services-qbhx5.ondigitalocean.app", httpClient);
            });
            
            // CAS service removed - storage handled externally
            
            // Register specific OCR services for each document type with keys
            services.AddKeyedScoped<IOCRService, ReceiptOCRService>(DocumentType.Receipt);
            services.AddKeyedScoped<IOCRService, CheckOCRService>(DocumentType.Check);
            
            // Also register them without keys for direct injection
            services.AddScoped<ReceiptOCRService>();
            services.AddScoped<CheckOCRService>();
            
            // Register IOCRService with a factory that routes to the appropriate service
            // For processors that specify document type, we'll use the appropriate service
            // This is mainly for backward compatibility - new code should use specific services
            services.AddScoped<IOCRService, OCRServiceRouter>();
            
            // Register supporting services that are still needed
            
            // Confidence score service for result validation
            services.AddScoped<IConfidenceScoreService, ConfidenceScoreService>();
            
            // Document processing queue for batch operations (in-memory only)
            services.AddSingleton<IBackgroundProcessingQueue, DocumentProcessingQueue>();
            
            // Wake lock service for keeping device awake during processing
            services.AddScoped<IWakeLockService, WakeLockService>();
            
            return services;
        }
        
        /// <summary>
        /// Adds minimal OCR services with just the core functionality
        /// </summary>
        public static IServiceCollection AddMinimalOCRServices(this IServiceCollection services)
        {
            // Configure HttpClient for MistralOCRClient without auto-registration
            services.AddHttpClient("MistralOCRClient", client =>
            {
                client.BaseAddress = new Uri("https://nolock-ocr-services-qbhx5.ondigitalocean.app");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            
            // Register MistralOCRClient as Scoped with configured HttpClient
            services.AddScoped<MistralOCRClient>(provider =>
            {
                var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("MistralOCRClient");
                return new MistralOCRClient("https://nolock-ocr-services-qbhx5.ondigitalocean.app", httpClient);
            });
            
            // CAS service removed - storage handled externally
            
            // Register specific OCR services for each document type with keys
            services.AddKeyedScoped<IOCRService, ReceiptOCRService>(DocumentType.Receipt);
            services.AddKeyedScoped<IOCRService, CheckOCRService>(DocumentType.Check);
            
            // Also register them without keys for direct injection
            services.AddScoped<ReceiptOCRService>();
            services.AddScoped<CheckOCRService>();
            
            // Register only the OCR service (for backward compatibility)
            services.AddScoped<IOCRService, OCRServiceRouter>();
            
            return services;
        }
    }
}