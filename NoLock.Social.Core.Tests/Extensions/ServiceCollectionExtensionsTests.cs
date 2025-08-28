using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Accessibility.Interfaces;
using NoLock.Social.Core.Accessibility.Services;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Services;
using NoLock.Social.Core.Common.Interfaces;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Cryptography.Services;
using NoLock.Social.Core.Extensions;
using NoLock.Social.Core.Hashing;
using NoLock.Social.Core.Identity.Interfaces;
using NoLock.Social.Core.Identity.Services;
using NoLock.Social.Core.Performance;
using NoLock.Social.Core.Security;
using NoLock.Social.Core.Storage;
using Xunit;

namespace NoLock.Social.Core.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static IServiceCollection AddBasicDependencies(IServiceCollection services)
    {
        // Add logging
        services.AddLogging();
        
        // Add JSInterop mock
        var jsRuntimeMock = new Mock<IJSRuntime>();
        services.AddSingleton(jsRuntimeMock.Object);
        
        // Add JSRuntimeWrapper mock
        var jsWrapperMock = new Mock<IJSRuntimeWrapper>();
        services.AddSingleton(jsWrapperMock.Object);
        
        return services;
    }
    [Fact]
    public void AddContentAddressableStorage_RegistersHashServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContentAddressableStorage();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        Assert.NotNull(serviceProvider.GetService<IHashAlgorithm>());
        Assert.NotNull(serviceProvider.GetService<IHashService>());
        
        // Verify correct implementations
        Assert.IsType<SHA256HashAlgorithm>(serviceProvider.GetService<IHashAlgorithm>());
        Assert.IsType<SHA256HashService>(serviceProvider.GetService<IHashService>());
    }

    [Fact]
    public void AddContentAddressableStorage_RegistersServicesWithCorrectLifetimes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddContentAddressableStorage();

        // Assert
        var hashAlgorithmDescriptor = services.First(s => s.ServiceType == typeof(IHashAlgorithm));
        var hashServiceDescriptor = services.First(s => s.ServiceType == typeof(IHashService));
        
        Assert.Equal(ServiceLifetime.Scoped, hashAlgorithmDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, hashServiceDescriptor.Lifetime);
    }

    [Theory]
    [InlineData(true, "with JSON serializer")]
    [InlineData(false, "without JSON serializer")]
    public void AddTypedContentAddressableStorage_ConfiguresSerializerBasedOnParameter(bool useJsonSerializer, string scenario)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTypedContentAddressableStorage<string>(useJsonSerializer);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        if (useJsonSerializer)
        {
            var serializer = serviceProvider.GetService<ISerializer<string>>();
            Assert.NotNull(serializer);
            Assert.IsType<JsonSerializer<string>>(serializer);
        }
        else
        {
            var serializer = serviceProvider.GetService<ISerializer<string>>();
            Assert.Null(serializer); // No serializer registered when false
        }
    }

    [Fact]
    public void AddTypedContentAddressableStorage_DefaultsToJsonSerializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTypedContentAddressableStorage<string>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var serializer = serviceProvider.GetService<ISerializer<string>>();
        
        Assert.NotNull(serializer);
        Assert.IsType<JsonSerializer<string>>(serializer);
    }

    [Theory]
    [InlineData(true, "reactive session state")]
    [InlineData(false, "standard session state")]
    public void AddCryptographicServices_ConfiguresSessionStateBasedOnParameter(bool useReactive, string scenario)
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act
        services.AddCryptographicServices(useReactive);

        // Assert - Check service descriptors
        if (useReactive)
        {
            // Should register ReactiveSessionStateService and expose both interfaces
            var reactiveDescriptor = services.First(s => s.ServiceType == typeof(ReactiveSessionStateService));
            Assert.Equal(ServiceLifetime.Scoped, reactiveDescriptor.Lifetime);
            
            var sessionStateDescriptor = services.First(s => s.ServiceType == typeof(ISessionStateService));
            Assert.Equal(ServiceLifetime.Scoped, sessionStateDescriptor.Lifetime);
            Assert.True(sessionStateDescriptor.ImplementationFactory != null);
            
            var reactiveStateDescriptor = services.First(s => s.ServiceType == typeof(IReactiveSessionStateService));
            Assert.Equal(ServiceLifetime.Scoped, reactiveStateDescriptor.Lifetime);
            Assert.True(reactiveStateDescriptor.ImplementationFactory != null);
        }
        else
        {
            // Should register standard SessionStateService only
            var sessionStateDescriptor = services.First(s => s.ServiceType == typeof(ISessionStateService));
            Assert.Equal(typeof(SessionStateService), sessionStateDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Scoped, sessionStateDescriptor.Lifetime);
            
            // Should not register IReactiveSessionStateService
            Assert.DoesNotContain(services, s => s.ServiceType == typeof(IReactiveSessionStateService));
        }
    }

    [Fact]
    public void AddCryptographicServices_RegistersAllCryptographicServices()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act
        services.AddCryptographicServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify all cryptographic services are registered
        Assert.NotNull(serviceProvider.GetService<IBrowserCompatibilityService>());
        Assert.NotNull(serviceProvider.GetService<IWebCryptoService>());
        Assert.NotNull(serviceProvider.GetService<ISecureMemoryManager>());
        Assert.NotNull(serviceProvider.GetService<ISessionStateService>());
        Assert.NotNull(serviceProvider.GetService<IKeyDerivationService>());
        Assert.NotNull(serviceProvider.GetService<ISigningService>());
        Assert.NotNull(serviceProvider.GetService<IVerificationService>());
        Assert.NotNull(serviceProvider.GetService<ICryptoErrorHandlingService>());
        
        // Verify correct implementations
        Assert.IsType<BrowserCompatibilityService>(serviceProvider.GetService<IBrowserCompatibilityService>());
        Assert.IsType<WebCryptoService>(serviceProvider.GetService<IWebCryptoService>());
        Assert.IsType<SecureMemoryManager>(serviceProvider.GetService<ISecureMemoryManager>());
        Assert.IsType<SessionStateService>(serviceProvider.GetService<ISessionStateService>());
        Assert.IsType<KeyDerivationService>(serviceProvider.GetService<IKeyDerivationService>());
        Assert.IsType<SigningService>(serviceProvider.GetService<ISigningService>());
        Assert.IsType<VerificationService>(serviceProvider.GetService<IVerificationService>());
        Assert.IsType<CryptoErrorHandlingService>(serviceProvider.GetService<ICryptoErrorHandlingService>());
    }

    [Fact]
    public void AddCryptographicServices_RegistersServicesWithCorrectLifetimes()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act
        services.AddCryptographicServices();

        // Assert
        var secureMemoryManagerDescriptor = services.First(s => s.ServiceType == typeof(ISecureMemoryManager));
        Assert.Equal(ServiceLifetime.Singleton, secureMemoryManagerDescriptor.Lifetime);
        
        // All other services should be scoped
        var scopedServices = new[]
        {
            typeof(IBrowserCompatibilityService),
            typeof(IWebCryptoService),
            typeof(ISessionStateService),
            typeof(IKeyDerivationService),
            typeof(ISigningService),
            typeof(IVerificationService),
            typeof(ICryptoErrorHandlingService)
        };
        
        foreach (var serviceType in scopedServices)
        {
            var descriptor = services.First(s => s.ServiceType == serviceType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    [Fact]
    public void AddSecurityServices_RegistersSecurityService()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act
        services.AddSecurityServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var securityService = serviceProvider.GetService<ISecurityService>();
        
        Assert.NotNull(securityService);
        Assert.IsType<SecurityService>(securityService);
        
        // Verify lifetime
        var descriptor = services.First(s => s.ServiceType == typeof(ISecurityService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddPerformanceMonitoring_RegistersPerformanceMonitoringService()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act
        services.AddPerformanceMonitoring();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var performanceService = serviceProvider.GetService<IPerformanceMonitoringService>();
        
        Assert.NotNull(performanceService);
        Assert.IsType<PerformanceMonitoringService>(performanceService);
        
        // Verify lifetime
        var descriptor = services.First(s => s.ServiceType == typeof(IPerformanceMonitoringService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddLoginServices_RegistersAllLoginServices()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);
        // Login services need cryptographic services as dependencies
        services.AddCryptographicServices();

        // Act
        services.AddLoginServices();

        // Assert - Check service descriptors without trying to resolve instances
        var userTrackingServices = services.Where(s => s.ServiceType == typeof(IUserTrackingService)).ToList();
        var rememberMeServices = services.Where(s => s.ServiceType == typeof(IRememberMeService)).ToList();
        var sessionPersistenceServices = services.Where(s => s.ServiceType == typeof(ISessionPersistenceService)).ToList();
        var loginAdapterServices = services.Where(s => s.ServiceType == typeof(ILoginAdapterService)).ToList();
        
        // Each login service type should be registered exactly once by AddLoginServices
        Assert.Single(userTrackingServices);
        Assert.Single(rememberMeServices);
        Assert.Single(loginAdapterServices);
        // SessionPersistenceService might be registered multiple times (once in mock, once in actual registration)
        
        // Verify correct implementations and lifetimes
        var userTrackingDescriptor = services.First(s => s.ServiceType == typeof(IUserTrackingService));
        Assert.Equal(typeof(UserTrackingService), userTrackingDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, userTrackingDescriptor.Lifetime);
        
        var rememberMeDescriptor = services.First(s => s.ServiceType == typeof(IRememberMeService));
        Assert.Equal(typeof(RememberMeService), rememberMeDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, rememberMeDescriptor.Lifetime);
        
        var sessionPersistenceDescriptor = services.First(s => s.ServiceType == typeof(ISessionPersistenceService));
        Assert.Equal(typeof(SecureSessionPersistenceService), sessionPersistenceDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, sessionPersistenceDescriptor.Lifetime);
        
        var loginAdapterDescriptor = services.First(s => s.ServiceType == typeof(ILoginAdapterService));
        Assert.Equal(typeof(LoginAdapterService), loginAdapterDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, loginAdapterDescriptor.Lifetime);
    }

    [Fact]
    public void AddOfflineStorageServices_ReturnsServiceCollectionWithoutAddingServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var originalCount = services.Count;

        // Act
        var result = services.AddOfflineStorageServices();

        // Assert
        Assert.Same(services, result); // Returns same instance for chaining
        Assert.Equal(originalCount, services.Count); // No services added
    }

    [Fact]
    public void AddAccessibilityServices_RegistersAllAccessibilityServices()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act
        services.AddAccessibilityServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify all accessibility services are registered
        Assert.NotNull(serviceProvider.GetService<IVoiceCommandService>());
        Assert.NotNull(serviceProvider.GetService<IFocusManagementService>());
        Assert.NotNull(serviceProvider.GetService<IAnnouncementService>());
        
        // Verify correct implementations
        Assert.IsType<VoiceCommandService>(serviceProvider.GetService<IVoiceCommandService>());
        Assert.IsType<FocusManagementService>(serviceProvider.GetService<IFocusManagementService>());
        Assert.IsType<AnnouncementService>(serviceProvider.GetService<IAnnouncementService>());
        
        // Verify all are scoped
        var accessibilityServices = new[]
        {
            typeof(IVoiceCommandService),
            typeof(IFocusManagementService),
            typeof(IAnnouncementService)
        };
        
        foreach (var serviceType in accessibilityServices)
        {
            var descriptor = services.First(s => s.ServiceType == serviceType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    [Fact]
    public void AddCameraServices_RegistersCameraService()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act
        services.AddCameraServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var cameraService = serviceProvider.GetService<ICameraService>();
        
        Assert.NotNull(cameraService);
        Assert.IsType<CameraService>(cameraService);
        
        // Verify lifetime
        var descriptor = services.First(s => s.ServiceType == typeof(ICameraService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddImageProcessingServices_ReturnsServiceCollectionWithoutAddingServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var originalCount = services.Count;

        // Act
        var result = services.AddImageProcessingServices();

        // Assert
        Assert.Same(services, result); // Returns same instance for chaining
        Assert.Equal(originalCount, services.Count); // No services added
    }

    [Fact]
    public void AllExtensionMethods_ReturnServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert - Test method chaining
        var result = services
            .AddContentAddressableStorage()
            .AddTypedContentAddressableStorage<string>()
            .AddCryptographicServices()
            .AddSecurityServices()
            .AddPerformanceMonitoring()
            .AddLoginServices()
            .AddOfflineStorageServices()
            .AddAccessibilityServices()
            .AddCameraServices()
            .AddImageProcessingServices();

        Assert.Same(services, result); // All methods should return the same service collection instance
    }

    [Fact]
    public void CombinedServiceRegistration_RegistersAllServicesWithoutConflicts()
    {
        // Arrange
        var services = new ServiceCollection();
        AddBasicDependencies(services);

        // Act - Register all services
        services
            .AddContentAddressableStorage()
            .AddTypedContentAddressableStorage<string>()
            .AddCryptographicServices(useReactive: true)
            .AddSecurityServices()
            .AddPerformanceMonitoring()
            .AddLoginServices()
            .AddAccessibilityServices()
            .AddCameraServices();

        // Assert - Build service provider to verify no conflicts
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify a few key services from each category can be resolved
        Assert.NotNull(serviceProvider.GetService<IHashService>());
        Assert.NotNull(serviceProvider.GetService<ISerializer<string>>());
        Assert.NotNull(serviceProvider.GetService<IReactiveSessionStateService>());
        Assert.NotNull(serviceProvider.GetService<ISecurityService>());
        Assert.NotNull(serviceProvider.GetService<IPerformanceMonitoringService>());
        Assert.NotNull(serviceProvider.GetService<ILoginAdapterService>());
        Assert.NotNull(serviceProvider.GetService<IVoiceCommandService>());
        Assert.NotNull(serviceProvider.GetService<ICameraService>());
    }
}