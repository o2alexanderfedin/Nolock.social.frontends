# PRP: IPFS Helia Filesystem Implementation

**Date**: 2025-09-12  
**Feature**: IPFS Helia Filesystem Integration  
**Architecture Document**: `/docs/architecture/ipfs-helia-filesystem.md`  
**Confidence Score**: 9/10 (One-pass implementation success)

## Executive Summary

Implement a complete IPFS decentralized filesystem for NoLock.Social using Helia with browser-native IndexedDB persistence. This vertical slice spans from Blazor UI components through C# Stream interfaces to JavaScript Helia UnixFS operations, providing users with infinite scalable storage capabilities.

## Context & Requirements

### Architecture Document Reference
Read the comprehensive architecture document at: `/Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/docs/architecture/ipfs-helia-filesystem.md`

This document contains:
- Complete vertical slice architecture alignment
- Technology overview (Helia, UnixFS, IndexedDB)
- Component diagrams and data flow
- C# Stream interface designs
- JavaScript implementation patterns
- 5-day implementation roadmap

### Core Requirements
1. **File System Operations**: Upload, download, list, mkdir, rm, stat
2. **Streaming Support**: Progressive chunk-based loading (256KB chunks)
3. **Persistent Storage**: IndexedDB for browser-native persistence
4. **C# Stream Interface**: Familiar .NET patterns (IpfsReadStream, IpfsWriteStream)
5. **Ultra-Minimal Overhead**: <300KB total bundle size increase
6. **Limited Hardware**: Must work efficiently on constrained devices

## External Documentation & Resources

### Official Helia Documentation
- **Main Site**: https://helia.io/
- **API Docs**: https://ipfs.github.io/helia/modules/helia.html
- **UnixFS API**: https://ipfs.github.io/helia/modules/_helia_unixfs.html
- **Tutorial**: https://github.com/ipfs-examples/helia-101/

### NPM Packages
- **helia**: https://www.npmjs.com/package/helia (core library)
- **@helia/unixfs**: https://www.npmjs.com/package/@helia/unixfs (filesystem operations)
- **blockstore-idb**: https://www.npmjs.com/package/blockstore-idb (IndexedDB blocks)
- **datastore-idb**: https://www.npmjs.com/package/datastore-idb (IndexedDB metadata)

## Codebase Patterns to Follow

### JavaScript Interop Pattern (from CameraService.cs)
```csharp
// Pattern from: /NoLock.Social.Core/Camera/Services/CameraService.cs
public class IpfsFileSystemService : IIpfsFileSystem, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<IpfsFileSystemService> _logger;
    private IJSObjectReference? _jsModule;
    private bool _disposed;

    public IpfsFileSystemService(IJSRuntime jsRuntime, ILogger<IpfsFileSystemService> logger)
    {
        _jsRuntime = Guard.AgainstNull(jsRuntime);
        _logger = logger;
    }

    public async ValueTask InitializeAsync()
    {
        _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/ipfs-helia.js");
        await _jsModule.InvokeVoidAsync("initialize");
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("cleanup");
            await _jsModule.DisposeAsync();
        }
    }
}
```

### JavaScript Module Pattern (from indexeddb-storage.js)
```javascript
// Pattern from: /NoLock.Social.Web/wwwroot/js/indexeddb-storage.js
window.ipfsHelia = (function() {
    'use strict';
    
    let helia = null;
    let fs = null;
    let isInitialized = false;
    
    async function initialize() {
        if (isInitialized && helia) {
            return;
        }
        // Initialize Helia with IndexedDB
    }
    
    return {
        initialize: initialize,
        uploadFile: uploadFile,
        downloadFile: downloadFile,
        // ... other methods
    };
})();
```

### Test Pattern (from CameraServiceTests.cs)
```csharp
// Pattern from: /NoLock.Social.Core.Tests/Camera/CameraServiceTests.cs
public class IpfsFileSystemServiceTests : IDisposable
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly Mock<ILogger<IpfsFileSystemService>> _loggerMock;
    private readonly IpfsFileSystemService _sut;

    public IpfsFileSystemServiceTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _loggerMock = new Mock<ILogger<IpfsFileSystemService>>();
        _sut = new IpfsFileSystemService(_jsRuntimeMock.Object, _loggerMock.Object);
    }
}
```

## Implementation Tasks (In Order)

### Phase 1: JavaScript Foundation (Day 1)
1. **Create ipfs-helia.js module** (`/NoLock.Social.Web/wwwroot/js/ipfs-helia.js`)
   - Initialize Helia with IndexedDB blockstore/datastore
   - Implement UnixFS operations (add, cat, ls, mkdir, rm)
   - Handle streaming with async iterables
   - Export functions for C# interop

2. **Add NPM packages** (`/NoLock.Social.Web/package.json`)
   ```json
   {
     "dependencies": {
       "helia": "^5.1.0",
       "@helia/unixfs": "^4.0.0",
       "blockstore-idb": "^2.0.0",
       "datastore-idb": "^3.0.0"
     }
   }
   ```

### Phase 2: C# Service Layer (Day 2)
3. **Implement IpfsFileSystemService** (`/NoLock.Social.Core/Storage/Ipfs/IpfsFileSystemService.cs`)
   - Implement IIpfsFileSystem interface (already created)
   - JavaScript module initialization
   - Async disposal pattern
   - Error handling and logging

4. **Complete Stream implementations**
   - Update IpfsReadStream.cs with JS interop calls
   - Update IpfsWriteStream.cs with buffering logic
   - Implement chunked reading/writing

### Phase 3: Integration & Testing (Day 3)
5. **Service registration** (`/NoLock.Social.Core/Extensions/ServiceCollectionExtensions.cs`)
   ```csharp
   services.AddScoped<IIpfsFileSystem, IpfsFileSystemService>();
   ```

6. **Create unit tests** (`/NoLock.Social.Core.Tests/Storage/Ipfs/IpfsFileSystemServiceTests.cs`)
   - Mock JS interop calls
   - Test stream operations
   - Verify disposal patterns
   - Test error handling

### Phase 4: UI Integration (Day 4)
7. **Create Blazor component** (`/NoLock.Social.Components/Storage/IpfsFileManager.razor`)
   - File upload/download UI
   - Directory listing
   - Progress indicators
   - Error feedback

8. **Component tests** (`/NoLock.Social.Components.Tests/Storage/IpfsFileManagerTests.cs`)
   - Use bUnit for component testing
   - Test user interactions
   - Verify event callbacks

### Phase 5: E2E & Optimization (Day 5)
9. **E2E tests** (`/NoLock.Social.E2E.Tests/IpfsIntegrationTests.cs`)
   - Use Playwright for browser automation
   - Test complete upload/download flow
   - Verify IndexedDB persistence

10. **Performance optimization**
    - Bundle size verification (<300KB)
    - Lazy loading of IPFS modules
    - Memory profiling

## Implementation Blueprint (Pseudocode)

### JavaScript Module Core
```javascript
// ipfs-helia.js
import { createHelia } from 'helia'
import { unixfs } from '@helia/unixfs'
import { IDBBlockstore } from 'blockstore-idb'
import { IDBDatastore } from 'datastore-idb'

let helia = null;
let fs = null;

export async function initialize() {
    const blockstore = new IDBBlockstore('nolock-blocks')
    const datastore = new IDBDatastore('nolock-data')
    
    await blockstore.open()
    await datastore.open()
    
    helia = await createHelia({
        blockstore,
        datastore
    })
    
    fs = unixfs(helia)
    console.log('IPFS Helia initialized with IndexedDB')
}

export async function uploadFile(path, uint8Array) {
    const cid = await fs.addBytes(uint8Array, {
        path: path
    })
    return cid.toString()
}

export async function* readFileChunks(cid) {
    for await (const chunk of fs.cat(cid)) {
        yield chunk
    }
}

export async function listDirectory(cid) {
    const entries = []
    for await (const entry of fs.ls(cid)) {
        entries.push({
            name: entry.name,
            type: entry.type,
            size: entry.size,
            cid: entry.cid.toString()
        })
    }
    return entries
}
```

### C# Service Core
```csharp
// IpfsFileSystemService.cs
public class IpfsFileSystemService : IIpfsFileSystem, IAsyncDisposable
{
    private const int ChunkSize = 256 * 1024; // 256KB
    private IJSObjectReference? _jsModule;
    
    public async Task<string> WriteFileAsync(string path, Stream stream, 
        IProgress<long>? progress = null, CancellationToken ct = default)
    {
        var buffer = new byte[ChunkSize];
        var chunks = new List<byte[]>();
        var totalBytes = 0L;
        
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            var chunk = new byte[bytesRead];
            Array.Copy(buffer, chunk, bytesRead);
            chunks.Add(chunk);
            
            totalBytes += bytesRead;
            progress?.Report(totalBytes);
        }
        
        var fullData = chunks.SelectMany(c => c).ToArray();
        var cid = await _jsModule.InvokeAsync<string>("uploadFile", path, fullData);
        return cid;
    }
    
    public async Task<Stream> ReadFileAsync(string cid, CancellationToken ct = default)
    {
        return new IpfsReadStream(_jsModule, cid, ChunkSize);
    }
}
```

## Validation Gates

### Build & Compile
```bash
# C# Build
cd /Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend
dotnet build --no-incremental

# TypeScript/JavaScript (if using TypeScript)
cd NoLock.Social.Web
npm install
npm run build
```

### Unit Tests
```bash
# Run all tests
dotnet test

# Run specific IPFS tests
dotnet test --filter "FullyQualifiedName~IpfsFileSystemServiceTests"
```

### Integration Tests
```bash
# Run E2E tests with Playwright
cd NoLock.Social.E2E.Tests
dotnet test
```

### Manual Verification Checklist
- [ ] File upload creates CID and stores in IndexedDB
- [ ] File download retrieves correct content
- [ ] Directory operations (mkdir, ls, rm) work
- [ ] Stream reading shows progress
- [ ] IndexedDB persists across browser sessions
- [ ] Memory usage stays constant during large file operations
- [ ] Bundle size increase <300KB

## Error Handling Strategy

### JavaScript Errors
- Wrap all Helia operations in try-catch
- Return structured error objects to C#
- Log errors with context (operation, CID, size)

### C# Errors
- Use Result<T> pattern for operations
- Specific exception types (IpfsConnectionException, IpfsStorageException)
- Graceful degradation when IPFS unavailable

### User Feedback
- Progress indicators for long operations
- Clear error messages (no technical jargon)
- Retry mechanisms for transient failures

## Common Pitfalls & Solutions

### Pitfall 1: Memory Issues with Large Files
**Solution**: Use streaming/chunking, never load full file in memory

### Pitfall 2: IndexedDB Quota Exceeded
**Solution**: Implement storage quota checking and user warnings

### Pitfall 3: CORS Issues with IPFS Gateways
**Solution**: Use local Helia node, not external gateways

### Pitfall 4: Slow Initial Connection
**Solution**: Lazy load Helia, show "Initializing storage..." message

### Pitfall 5: Browser Compatibility
**Solution**: Check for IndexedDB support, provide fallback message

## Success Criteria

1. ✅ All file operations work (upload, download, list, mkdir, rm)
2. ✅ Streaming with progress indicators
3. ✅ IndexedDB persistence verified
4. ✅ Bundle size <300KB increase
5. ✅ All tests passing (unit, integration, E2E)
6. ✅ Works on iOS Safari and Android Chrome
7. ✅ Memory efficient (O(chunk size) not O(file size))
8. ✅ Graceful error handling
9. ✅ Clean disposal of resources
10. ✅ Documentation updated

## Additional Notes

### Security Considerations
- Never expose raw IPFS API to untrusted code
- Validate all file paths and CIDs
- Implement size limits for uploads
- Consider encryption for sensitive data

### Performance Tips
- Use Web Workers for large file processing
- Implement caching layer for frequently accessed files
- Consider CDN for initial Helia bundle loading
- Monitor IndexedDB usage and implement cleanup

### Future Enhancements (Not in MVP)
- IPNS for mutable references
- Pinning service integration
- File encryption/decryption
- Collaborative features (shared directories)
- IPFS pubsub for real-time updates

## References

- [Helia Documentation](https://helia.io/)
- [UnixFS API Reference](https://ipfs.github.io/helia/modules/_helia_unixfs.html)
- [Helia 101 Tutorial](https://github.com/ipfs-examples/helia-101/)
- [IndexedDB Blockstore](https://www.npmjs.com/package/blockstore-idb)
- [IndexedDB Datastore](https://www.npmjs.com/package/datastore-idb)
- Architecture Document: `/docs/architecture/ipfs-helia-filesystem.md`
- Existing JS Interop: `/NoLock.Social.Web/wwwroot/js/`
- Service Patterns: `/NoLock.Social.Core/Camera/Services/`
- Test Patterns: `/NoLock.Social.Core.Tests/`

---

**Implementation Ready**: This PRP contains all necessary context for one-pass implementation success. The architecture is validated as a true vertical slice, patterns are established in the codebase, and external documentation is referenced.