# Nolock.Social.Storage.IndexedDb

A thin wrapper library implementing Content Addressable Storage (CAS) on top of IndexedDB for Blazor WebAssembly applications.

## Features

- **Content Addressable Storage**: Store and retrieve data using SHA256 content hashes
- **Blazor WASM Compatible**: Built specifically for Blazor WebAssembly applications
- **Thin JavaScript Interop**: Minimal JS interop using BlazorEssentials.IndexedDb package
- **Type-Safe**: Generic interface with strong typing support
- **Async/Await**: Full async support for all operations

## Installation

Add the package reference to your Blazor WebAssembly project:

```xml
<PackageReference Include="Nolock.Social.Storage.IndexedDb" Version="1.0.0" />
```

## Usage

### 1. Register the service in Program.cs

```csharp
using Nolock.Social.Storage.IndexedDb;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add IndexedDB Content Addressable Storage
builder.Services.AddIndexedDbContentAddressableStorage();

await builder.Build().RunAsync();
```

### 2. Initialize and use the storage

```csharp
@inject IJSRuntime JSRuntime
@code {
    private IndexedDbContentAddressableStorage? _storage;

    protected override async Task OnInitializedAsync()
    {
        // Create and initialize the storage
        _storage = await ServiceCollectionExtensions.CreateAndInitializeAsync(JSRuntime);
        
        // Store data
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        var hash = await _storage.StoreAsync(data);
        Console.WriteLine($"Stored with hash: {hash}");
        
        // Retrieve data
        var retrieved = await _storage.GetAsync(hash);
        if (retrieved != null)
        {
            var text = Encoding.UTF8.GetString(retrieved);
            Console.WriteLine($"Retrieved: {text}");
        }
        
        // Check existence
        var exists = await _storage.ExistsAsync(hash);
        Console.WriteLine($"Exists: {exists}");
        
        // Delete data
        var deleted = await _storage.DeleteAsync(hash);
        Console.WriteLine($"Deleted: {deleted}");
    }
}
```

### 3. Advanced usage with enumeration

```csharp
// Enumerate all stored items
var allItems = await _storage.EnumerateAsync();
await foreach (var item in allItems)
{
    // Process each item
    Console.WriteLine($"Item size: {item.Length} bytes");
}

// Count items
var count = await _storage.CountAsync();
Console.WriteLine($"Total items: {count}");
```

## API Reference

### IContentAddressableStorage<T>

```csharp
public interface IContentAddressableStorage<T> where T : class
{
    Task<string> StoreAsync(T entity, CancellationToken cancellation = default);
    Task<T?> GetAsync(string contentHash, CancellationToken cancellation = default);
    Task<bool> ExistsAsync(string contentHash, CancellationToken cancellation = default);
    Task<bool> DeleteAsync(string contentHash, CancellationToken cancellation = default);
    Task<IAsyncEnumerable<T>> EnumerateAsync(Expression<Func<T, bool>>? filter = null, int? limit = null, int? offset = null, CancellationToken cancellation = default);
    Task<long> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellation = default);
}
```

## Architecture

The library uses:
- **BlazorEssentials.IndexedDb**: For IndexedDB access
- **SHA256**: For content addressing
- **System.Text.Json**: For serialization

## Browser Compatibility

This library requires browser support for IndexedDB:
- Chrome 24+
- Firefox 16+
- Safari 8+
- Edge 12+

## License

MIT

## Dependencies

- .NET 8.0
- BlazorEssentials.IndexedDb 3.0.0
- Microsoft.AspNetCore.Components.WebAssembly 8.0+
## Architecture Documentation

For detailed architecture and design decisions, see [Content Addressable Storage Design](../docs/architecture/content-addressable-storage-design.md)
