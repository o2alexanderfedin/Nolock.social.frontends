using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Configuration;
using NoLock.Social.Core.OCR.Processors;
using NoLock.Social.Core.OCR.Models;
using System;
using System.Net.Http;

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
            
            // Register HttpClient for OCR service
            services.AddHttpClient<OCRService>((serviceProvider, client) =>
            {
                // Set base URL for Mistral OCR API
                client.BaseAddress = new Uri("https://nolock-ocr-services-qbhx5.ondigitalocean.app");
                
                // Set default timeout (30 seconds)
                client.Timeout = TimeSpan.FromSeconds(30);
                
                // Add default headers if needed
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "NoLock-OCR-Client/1.0");
            });
            
            // Register the OCR service as IOCRService
            services.AddScoped<IOCRService, OCRService>();
            
            // Register supporting services that are still needed
            
            // Document processors for specific document types
            services.AddScoped<IDocumentProcessor, ReceiptProcessor>();
            services.AddScoped<IDocumentProcessor, CheckProcessor>();
            
            // Document processor registry
            services.AddScoped<IDocumentProcessorRegistry>(provider =>
            {
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DocumentProcessorRegistry>>();
                var registry = new DocumentProcessorRegistry(logger, (IDocumentTypeDetector?)null); // Explicitly cast to avoid ambiguity
                
                // Register processors
                var processors = provider.GetServices<IDocumentProcessor>();
                foreach (var processor in processors)
                {
                    registry.RegisterProcessor(processor);
                }
                
                return registry;
            });
            
            // Confidence score service for result validation
            services.AddScoped<IConfidenceScoreService, ConfidenceScoreService>();
            
            // Document processing queue for batch operations
            services.AddSingleton<IBackgroundProcessingQueue, DocumentProcessingQueue>();
            
            // Wake lock service for keeping device awake during processing
            services.AddScoped<IWakeLockService, WakeLockService>();
            
            // OCR polling service for checking status
            services.AddScoped<IOCRPollingService, OCRPollingService>();
            
            return services;
        }
        
        /// <summary>
        /// Adds minimal OCR services with just the core functionality
        /// </summary>
        public static IServiceCollection AddMinimalOCRServices(this IServiceCollection services)
        {
            // Register HttpClient for OCR service with minimal configuration
            services.AddHttpClient<OCRService>((serviceProvider, client) =>
            {
                client.BaseAddress = new Uri("https://nolock-ocr-services-qbhx5.ondigitalocean.app");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            
            // Register only the OCR service
            services.AddScoped<IOCRService, OCRService>();
            
            return services;
        }
    }
}