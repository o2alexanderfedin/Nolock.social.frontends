using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NoLock.Social.Core.Storage;
using NoLock.Social.Core.Hashing;
using Nolock.Social.Storage.IndexedDb;
using Xunit;
using Moq;

namespace Nolock.Social.Storage.IndexedDb.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIndexedDbContentAddressableStorage_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Add required dependencies first
        services.AddSingleton<IJSRuntime>(new MockJSRuntime());
        services.AddSingleton<IHashService>(Mock.Of<IHashService>());
        
        // Act
        var result = services.AddIndexedDbContentAddressableStorage<string>();
        
        // Assert
        Assert.Same(services, result); // Fluent interface returns same collection
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify IndexedDbCasDatabase is registered as Scoped
        Assert.NotNull(serviceProvider.GetService<IndexedDbCasDatabase>());
        var db1 = serviceProvider.GetService<IndexedDbCasDatabase>();
        var db2 = serviceProvider.GetService<IndexedDbCasDatabase>();
        Assert.Same(db1, db2); // Same instance within scope
        
        // Verify ISerializer<T> is registered as Scoped
        Assert.NotNull(serviceProvider.GetService<ISerializer<string>>());
        var serializer1 = serviceProvider.GetService<ISerializer<string>>();
        var serializer2 = serviceProvider.GetService<ISerializer<string>>();
        Assert.Same(serializer1, serializer2); // Same instance within scope
        
        // Verify IndexedDbContentAddressableStorage<T> is registered as Scoped
        Assert.NotNull(serviceProvider.GetService<IndexedDbContentAddressableStorage<string>>());
        var storage1 = serviceProvider.GetService<IndexedDbContentAddressableStorage<string>>();
        var storage2 = serviceProvider.GetService<IndexedDbContentAddressableStorage<string>>();
        Assert.Same(storage1, storage2); // Same instance within scope
        
        // Verify IContentAddressableStorage<T> is registered as Scoped
        Assert.NotNull(serviceProvider.GetService<IContentAddressableStorage<string>>());
        var interface1 = serviceProvider.GetService<IContentAddressableStorage<string>>();
        var interface2 = serviceProvider.GetService<IContentAddressableStorage<string>>();
        Assert.Same(interface1, interface2); // Same instance within scope
        
        // Verify interface resolves to concrete implementation
        Assert.IsType<IndexedDbContentAddressableStorage<string>>(interface1);
    }
    
    [Theory]
    [InlineData(typeof(string), "string type")]
    [InlineData(typeof(int), "int type")]
    [InlineData(typeof(object), "object type")]
    public void AddIndexedDbContentAddressableStorage_WorksWithDifferentGenericTypes(Type genericType, string scenario)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new MockJSRuntime());
        services.AddSingleton<IHashService>(Mock.Of<IHashService>());
        
        // Act - Use reflection to call generic method
        var method = typeof(ServiceCollectionExtensions)
            .GetMethod(nameof(ServiceCollectionExtensions.AddIndexedDbContentAddressableStorage))!
            .MakeGenericMethod(genericType);
        
        var result = method.Invoke(null, new object[] { services });
        
        // Assert
        Assert.Same(services, result);
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify services are registered for the specific generic type
        var serializerType = typeof(ISerializer<>).MakeGenericType(genericType);
        var storageType = typeof(IContentAddressableStorage<>).MakeGenericType(genericType);
        
        Assert.NotNull(serviceProvider.GetService(serializerType));
        Assert.NotNull(serviceProvider.GetService(storageType));
    }
    
    [Fact]
    public void AddIndexedDbContentAddressableStorage_CanBeCalledMultipleTimes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new MockJSRuntime());
        services.AddSingleton<IHashService>(Mock.Of<IHashService>());
        
        // Act - Call multiple times with different types
        services.AddIndexedDbContentAddressableStorage<string>();
        services.AddIndexedDbContentAddressableStorage<int>();
        services.AddIndexedDbContentAddressableStorage<object>();
        
        // Assert - Should not throw and services should be registered
        var serviceProvider = services.BuildServiceProvider();
        
        Assert.NotNull(serviceProvider.GetService<IContentAddressableStorage<string>>());
        Assert.NotNull(serviceProvider.GetService<IContentAddressableStorage<int>>());
        Assert.NotNull(serviceProvider.GetService<IContentAddressableStorage<object>>());
        
        // Each type should have its own registration
        Assert.NotSame(
            serviceProvider.GetService<IContentAddressableStorage<string>>(),
            serviceProvider.GetService<IContentAddressableStorage<int>>()
        );
    }
    
    [Fact]
    public void AddIndexedDbContentAddressableStorage_RegistersJsonSerializerImplementation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new MockJSRuntime());
        services.AddSingleton<IHashService>(Mock.Of<IHashService>());
        
        // Act
        services.AddIndexedDbContentAddressableStorage<string>();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var serializer = serviceProvider.GetService<ISerializer<string>>();
        
        Assert.IsType<JsonSerializer<string>>(serializer);
    }
    
    [Fact]
    public void AddIndexedDbContentAddressableStorage_AllServicesHaveScopedLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new MockJSRuntime());
        services.AddSingleton<IHashService>(Mock.Of<IHashService>());
        
        // Act
        services.AddIndexedDbContentAddressableStorage<string>();
        
        // Assert - Verify all services are registered as Scoped
        var serviceDescriptors = services.Where(s => 
            s.ServiceType == typeof(IndexedDbCasDatabase) ||
            s.ServiceType == typeof(ISerializer<string>) ||
            s.ServiceType == typeof(IndexedDbContentAddressableStorage<string>) ||
            s.ServiceType == typeof(IContentAddressableStorage<string>)
        ).ToList();
        
        Assert.All(serviceDescriptors, descriptor => 
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }
}

public class MockJSRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return ValueTask.FromResult(default(TValue)!);
    }
}