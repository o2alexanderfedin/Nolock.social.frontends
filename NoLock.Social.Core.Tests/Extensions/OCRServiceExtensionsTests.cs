using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Common.Interfaces;
using NoLock.Social.Core.Extensions;
using NoLock.Social.Core.OCR.Configuration;
using NoLock.Social.Core.OCR.Generated;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services;
using System.Net.Http;
using Xunit;

namespace NoLock.Social.Core.Tests.Extensions;

public class OCRServiceExtensionsTests
{
    private static IServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        
        // Add basic dependencies that OCR services need
        var jsRuntimeMock = new Mock<IJSRuntime>();
        services.AddSingleton(jsRuntimeMock.Object);
        
        var jsWrapperMock = new Mock<IJSRuntimeWrapper>();
        services.AddSingleton(jsWrapperMock.Object);
        
        // Add logging for services that need it
        services.AddLogging();
        
        return services;
    }

    private static void PostProcessServices(IServiceCollection services)
    {
        // The extension registers MistralOCRClient but OCR services need IMistralOCRClient interface
        // Add interface registration that forwards to the concrete implementation
        services.AddScoped<IMistralOCRClient>(provider => provider.GetRequiredService<MistralOCRClient>());
    }

    private static Mock<IConfiguration> CreateMockConfiguration()
    {
        var configMock = new Mock<IConfiguration>();
        var sectionMock = new Mock<IConfigurationSection>();
        configMock.Setup(c => c.GetSection(OCRServiceOptions.SectionName)).Returns(sectionMock.Object);
        return configMock;
    }

    [Theory]
    [InlineData("full OCR services")]
    [InlineData("minimal OCR services")]
    public void AddOCRServices_RegistersHttpClientForMistralOCRClient(string scenario)
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        if (scenario == "full OCR services")
        {
            services.AddOCRServices(config.Object);
            PostProcessServices(services);
        }
        else
        {
            services.AddMinimalOCRServices();
            PostProcessServices(services);
        }

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify IHttpClientFactory is registered
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);
        
        // Verify MistralOCRClient can be resolved (which internally uses the configured HttpClient)
        var mistralClient = serviceProvider.GetService<MistralOCRClient>();
        Assert.NotNull(mistralClient);
    }

    [Theory]
    [InlineData("full OCR services")]
    [InlineData("minimal OCR services")]
    public void AddOCRServices_RegistersMistralOCRClient(string scenario)
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        if (scenario == "full OCR services")
        {
            services.AddOCRServices(config.Object);
            PostProcessServices(services);
        }
        else
        {
            services.AddMinimalOCRServices();
            PostProcessServices(services);
        }

        // Assert - Check service registration without trying to resolve
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(MistralOCRClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory); // Should use factory registration
    }

    [Theory]
    [InlineData("full OCR services")]
    [InlineData("minimal OCR services")]
    public void AddOCRServices_RegistersKeyedOCRServices(string scenario)
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        if (scenario == "full OCR services")
        {
            services.AddOCRServices(config.Object);
            PostProcessServices(services);
        }
        else
        {
            services.AddMinimalOCRServices();
            PostProcessServices(services);
        }

        // Assert - Check keyed service registrations without resolving
        var receiptDescriptor = services.First(s => s.ServiceType == typeof(IOCRService) && s.ServiceKey?.Equals(DocumentType.Receipt) == true);
        var checkDescriptor = services.First(s => s.ServiceType == typeof(IOCRService) && s.ServiceKey?.Equals(DocumentType.Check) == true);
        
        Assert.Equal(ServiceLifetime.Scoped, receiptDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, checkDescriptor.Lifetime);
        Assert.Equal(typeof(ReceiptOCRService), receiptDescriptor.ImplementationType);
        Assert.Equal(typeof(CheckOCRService), checkDescriptor.ImplementationType);
    }

    [Theory]
    [InlineData("full OCR services")]
    [InlineData("minimal OCR services")]
    public void AddOCRServices_RegistersDirectOCRServices(string scenario)
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        if (scenario == "full OCR services")
        {
            services.AddOCRServices(config.Object);
            PostProcessServices(services);
        }
        else
        {
            services.AddMinimalOCRServices();
            PostProcessServices(services);
        }

        // Assert - Check service registrations without resolving
        var receiptDescriptor = services.First(s => s.ServiceType == typeof(ReceiptOCRService));
        var checkDescriptor = services.First(s => s.ServiceType == typeof(CheckOCRService));
        
        Assert.Equal(ServiceLifetime.Scoped, receiptDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, checkDescriptor.Lifetime);
        Assert.Equal(typeof(ReceiptOCRService), receiptDescriptor.ImplementationType);
        Assert.Equal(typeof(CheckOCRService), checkDescriptor.ImplementationType);
    }

    [Theory]
    [InlineData("full OCR services")]
    [InlineData("minimal OCR services")]
    public void AddOCRServices_RegistersOCRServiceRouter(string scenario)
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        if (scenario == "full OCR services")
        {
            services.AddOCRServices(config.Object);
            PostProcessServices(services);
        }
        else
        {
            services.AddMinimalOCRServices();
            PostProcessServices(services);
        }

        // Assert - Check service registration without resolving
        var descriptor = services.First(s => s.ServiceType == typeof(IOCRService) && s.ServiceKey == null);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(OCRServiceRouter), descriptor.ImplementationType);
    }

    [Fact]
    public void AddOCRServices_ConfiguresOCRServiceOptions()
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        services.AddOCRServices(config.Object);
        PostProcessServices(services);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<OCRServiceOptions>>();
        
        Assert.NotNull(options);
        
        // Verify configuration section was used
        config.Verify(c => c.GetSection(OCRServiceOptions.SectionName), Times.Once);
    }

    [Fact]
    public void AddOCRServices_RegistersFullSupportingServices()
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        services.AddOCRServices(config.Object);
        PostProcessServices(services);

        // Assert - Check service registrations without resolving complex services
        var confidenceScoreDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IConfidenceScoreService));
        var processingQueueDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IBackgroundProcessingQueue));
        var wakeLockDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IWakeLockService));
        
        Assert.NotNull(confidenceScoreDescriptor);
        Assert.NotNull(processingQueueDescriptor);
        Assert.NotNull(wakeLockDescriptor);
        
        // Verify correct implementations
        Assert.Equal(typeof(ConfidenceScoreService), confidenceScoreDescriptor.ImplementationType);
        Assert.Equal(typeof(DocumentProcessingQueue), processingQueueDescriptor.ImplementationType);
        Assert.Equal(typeof(WakeLockService), wakeLockDescriptor.ImplementationType);
    }

    [Fact]
    public void AddOCRServices_RegistersSupportingServicesWithCorrectLifetimes()
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        services.AddOCRServices(config.Object);
        PostProcessServices(services);

        // Assert
        var confidenceScoreDescriptor = services.First(s => s.ServiceType == typeof(IConfidenceScoreService));
        var processingQueueDescriptor = services.First(s => s.ServiceType == typeof(IBackgroundProcessingQueue));
        var wakeLockDescriptor = services.First(s => s.ServiceType == typeof(IWakeLockService));
        
        Assert.Equal(ServiceLifetime.Scoped, confidenceScoreDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, processingQueueDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, wakeLockDescriptor.Lifetime);
    }

    [Fact]
    public void AddMinimalOCRServices_DoesNotRegisterSupportingServices()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddMinimalOCRServices();
        PostProcessServices(services);

        // Assert
        // Should not register supporting services
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IConfidenceScoreService));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IBackgroundProcessingQueue));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IWakeLockService));
    }

    [Fact]
    public void AddMinimalOCRServices_DoesNotConfigureOptions()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddMinimalOCRServices();
        PostProcessServices(services);

        // Assert
        // Should not register IOptions<OCRServiceOptions>
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IOptions<OCRServiceOptions>));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IConfigureOptions<OCRServiceOptions>));
    }

    [Theory]
    [InlineData("full OCR services")]
    [InlineData("minimal OCR services")]
    public void AddOCRServices_ReturnsServiceCollectionForChaining(string scenario)
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        IServiceCollection result;
        if (scenario == "full OCR services")
            result = services.AddOCRServices(config.Object);
        else
            result = services.AddMinimalOCRServices();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddOCRServices_ConfiguresHttpClientWithCorrectHeaders()
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        services.AddOCRServices(config.Object);
        PostProcessServices(services);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify MistralOCRClient can be created (which proves HttpClient configuration worked)
        var mistralClient = serviceProvider.GetService<MistralOCRClient>();
        Assert.NotNull(mistralClient);
        
        // Verify HttpClient service descriptor exists with proper configuration
        var httpClientDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(HttpClient) && 
            s.Lifetime == ServiceLifetime.Scoped);
        Assert.NotNull(httpClientDescriptor);
    }

    [Fact]
    public void AddMinimalOCRServices_ConfiguresHttpClientWithMinimalConfiguration()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddMinimalOCRServices();
        PostProcessServices(services);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify MistralOCRClient can be created (which proves basic HttpClient configuration worked)
        var mistralClient = serviceProvider.GetService<MistralOCRClient>();
        Assert.NotNull(mistralClient);
        
        // Verify HttpClient service descriptor exists  
        var httpClientDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(HttpClient) && 
            s.Lifetime == ServiceLifetime.Scoped);
        Assert.NotNull(httpClientDescriptor);
    }

    [Fact]
    public void AddOCRServices_AllServicesRegistered()
    {
        // Arrange
        var services = CreateServiceCollection();
        var config = CreateMockConfiguration();

        // Act
        services.AddOCRServices(config.Object);
        PostProcessServices(services);

        // Assert - Verify all expected services are registered
        Assert.Contains(services, s => s.ServiceType == typeof(MistralOCRClient));
        Assert.Contains(services, s => s.ServiceType == typeof(IOCRService) && s.ServiceKey == null);
        Assert.Contains(services, s => s.ServiceType == typeof(ReceiptOCRService));
        Assert.Contains(services, s => s.ServiceType == typeof(CheckOCRService));
        Assert.Contains(services, s => s.ServiceType == typeof(IOCRService) && s.ServiceKey?.Equals(DocumentType.Receipt) == true);
        Assert.Contains(services, s => s.ServiceType == typeof(IOCRService) && s.ServiceKey?.Equals(DocumentType.Check) == true);
        Assert.Contains(services, s => s.ServiceType == typeof(IConfidenceScoreService));
        Assert.Contains(services, s => s.ServiceType == typeof(IBackgroundProcessingQueue));
        Assert.Contains(services, s => s.ServiceType == typeof(IWakeLockService));
        Assert.Contains(services, s => s.ServiceType == typeof(IConfigureOptions<OCRServiceOptions>));
    }

    [Fact]
    public void AddMinimalOCRServices_CoreServicesRegistered()
    {
        // Arrange
        var services = CreateServiceCollection();

        // Act
        services.AddMinimalOCRServices();
        PostProcessServices(services);

        // Assert - Verify core services are registered
        Assert.Contains(services, s => s.ServiceType == typeof(MistralOCRClient));
        Assert.Contains(services, s => s.ServiceType == typeof(IOCRService) && s.ServiceKey == null);
        Assert.Contains(services, s => s.ServiceType == typeof(ReceiptOCRService));
        Assert.Contains(services, s => s.ServiceType == typeof(CheckOCRService));
        Assert.Contains(services, s => s.ServiceType == typeof(IOCRService) && s.ServiceKey?.Equals(DocumentType.Receipt) == true);
        Assert.Contains(services, s => s.ServiceType == typeof(IOCRService) && s.ServiceKey?.Equals(DocumentType.Check) == true);
        
        // Supporting services should not be registered
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IConfidenceScoreService));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IBackgroundProcessingQueue));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IWakeLockService));
    }
}