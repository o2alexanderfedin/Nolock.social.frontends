# IPFS MFS Stream Integration - Corrected Design

## Critical Correction: Stream-Based API

### Problem Identified
The current `IpfsFileSystemService` implementation has incorrect method signatures that don't align with the stream-based architecture:
- `WriteFileAsync` returns `Task<string>` instead of returning a stream
- `ReadFileAsync` returns generic `Task<Stream>` instead of specific `Task<IpfsReadStream>`

### Corrected C# Interface

```csharp
namespace NoLock.Social.Core.Storage.Ipfs
{
    /// <summary>
    /// IPFS file system operations with MFS (Mutable File System) support.
    /// </summary>
    public interface IIpfsFileSystem
    {
        /// <summary>
        /// Creates a write stream for uploading content to an MFS path.
        /// </summary>
        /// <param name="path">MFS path (e.g., "/documents/report.pdf")</param>
        /// <param name="progress">Optional progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>IpfsWriteStream for progressive writing</returns>
        Task<IpfsWriteStream> CreateWriteStreamAsync(
            string path, 
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a read stream for downloading content from an MFS path or CID.
        /// </summary>
        /// <param name="path">MFS path or CID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>IpfsReadStream for progressive reading</returns>
        Task<IpfsReadStream> OpenReadStreamAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists directory contents in MFS.
        /// </summary>
        Task<IEnumerable<IpfsFileEntry>> ListDirectoryAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file or directory exists in MFS.
        /// </summary>
        Task<bool> ExistsAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata about a file or directory.
        /// </summary>
        Task<IpfsFileMetadata?> GetMetadataAsync(
            string path, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes (unpins) a file from IPFS.
        /// </summary>
        Task<bool> DeleteAsync(
            string path, 
            CancellationToken cancellationToken = default);
    }
}
```

## Stream Implementation Details

### IpfsWriteStream (Existing)
- **Purpose**: Progressive upload to MFS path
- **Key Features**:
  - Chunked writing (256KB chunks)
  - Progress reporting
  - Returns CID via `CompleteAsync()` or `ResultCid` property
- **JavaScript Mapping**: Maps to MFS write operations

### IpfsReadStream (Existing)
- **Purpose**: Progressive download from MFS path or CID
- **Key Features**:
  - Chunked reading (256KB chunks)
  - Internal buffering for efficiency
  - Forward-only streaming
- **JavaScript Mapping**: Maps to MFS/UnixFS cat operations

## JavaScript Interop Corrections

### Write Stream Creation
```javascript
// ipfs-module.js
export async function createWriteStream(path) {
    // For MFS: prepare write to mutable path
    return {
        writeChunk: async (chunk) => {
            // Accumulate chunks for MFS write
            accumulator.push(chunk);
        },
        complete: async () => {
            // Write accumulated data to MFS path
            const content = concatenateChunks(accumulator);
            await ipfs.files.write(path, content, { create: true, parents: true });
            
            // Get the CID of the written file
            const stat = await ipfs.files.stat(path);
            return stat.cid.toString();
        },
        dispose: () => {
            // Clean up resources
            accumulator = [];
        }
    };
}
```

### Read Stream Creation
```javascript
// ipfs-module.js
export async function createReadStream(path) {
    // Determine if path is CID or MFS path
    const isPath = path.startsWith('/');
    
    // Get file size for stream initialization
    const stat = isPath 
        ? await ipfs.files.stat(path)
        : await ipfs.dag.get(CID.parse(path));
    
    // Create async iterator for reading
    const iterator = isPath
        ? ipfs.files.read(path)  // MFS read
        : ipfs.cat(path);         // CID read
    
    return {
        size: stat.size,
        readChunk: async (maxBytes) => {
            const { value, done } = await iterator.next();
            if (done) return new Uint8Array(0);
            
            // Limit chunk size if needed
            return value.length > maxBytes 
                ? value.slice(0, maxBytes)
                : value;
        },
        dispose: () => {
            if (iterator.return) iterator.return();
        }
    };
}
```

## Usage Patterns

### Writing to MFS
```csharp
// Create stream for MFS path
await using var writeStream = await ipfs.CreateWriteStreamAsync("/documents/report.pdf");

// Write content progressively
await sourceStream.CopyToAsync(writeStream);

// Complete and get CID
var cid = await writeStream.CompleteAsync();
```

### Reading from MFS
```csharp
// Open stream from MFS path
await using var readStream = await ipfs.OpenReadStreamAsync("/documents/report.pdf");

// Read content progressively
await readStream.CopyToAsync(destinationStream);
```

### Reading by CID
```csharp
// Open stream from CID (backward compatibility)
await using var readStream = await ipfs.OpenReadStreamAsync("QmXyz...");

// Read content progressively
await readStream.CopyToAsync(destinationStream);
```

## Benefits of Corrected Design

1. **Consistent Stream API**: Both read and write operations return streams
2. **Progressive Operations**: No need to load entire files in memory
3. **MFS Integration**: Seamless support for mutable paths
4. **Type Safety**: Specific stream types with appropriate capabilities
5. **Resource Management**: Proper disposal through IAsyncDisposable

## Implementation Priority

1. **Update Interface**: Change method signatures to return streams
2. **Modify Service**: Update `IpfsFileSystemService` implementation
3. **JavaScript Layer**: Implement MFS-aware stream creation
4. **Testing**: Verify both MFS paths and CID access work correctly

## Testing & Verification

### Unit Tests for Stream Operations

```csharp
// IpfsWriteStreamTests.cs
[Fact]
public async Task WriteStream_ChunkedWrite_MaintainsConstantMemory()
{
    // Arrange
    var mockJs = new Mock<IJSObjectReference>();
    var stream = new IpfsWriteStream(mockJs.Object, "/test/file.txt");
    var data = new byte[5 * 1024 * 1024]; // 5MB test data
    
    // Act - Write in chunks
    for (int i = 0; i < data.Length; i += 256 * 1024)
    {
        var chunk = data.Skip(i).Take(256 * 1024).ToArray();
        await stream.WriteAsync(chunk);
    }
    
    // Assert - Verify chunks were sent individually
    mockJs.Verify(js => js.InvokeAsync<IJSObjectReference>(
        "writeChunk", It.IsAny<object[]>()), 
        Times.Exactly(20)); // 5MB / 256KB = 20 chunks
}

[Fact]
public async Task ReadStream_PartialRead_ReturnsRequestedBytes()
{
    // Arrange
    var mockJs = new Mock<IJSObjectReference>();
    mockJs.Setup(js => js.InvokeAsync<byte[]>("readChunk", It.IsAny<object[]>()))
          .ReturnsAsync(new byte[1024]);
    
    var stream = new IpfsReadStream(mockJs.Object, 10240); // 10KB file
    var buffer = new byte[512];
    
    // Act
    var bytesRead = await stream.ReadAsync(buffer, 0, 512);
    
    // Assert
    Assert.Equal(512, bytesRead);
    Assert.Equal(512, stream.Position);
}

[Theory]
[InlineData("/documents/file.pdf", true)]  // MFS path
[InlineData("QmXyz123...", false)]         // CID
[InlineData("/images/photo.jpg", true)]    // MFS path
public async Task FileSystem_PathDetection_CorrectlyIdentifiesType(
    string path, bool isMfsPath)
{
    // Arrange
    var service = new IpfsFileSystemService(JSRuntime);
    
    // Act
    var stream = await service.OpenReadStreamAsync(path);
    
    // Assert
    Assert.Equal(isMfsPath, stream.IsMfsPath);
}
```

### Integration Tests for MFS Operations

```csharp
// IpfsFileSystemIntegrationTests.cs
[Fact]
public async Task MFS_WriteAndRead_RoundTrip()
{
    // Arrange
    var service = new IpfsFileSystemService(JSRuntime);
    var testData = Encoding.UTF8.GetBytes("Test content");
    var path = $"/test/{Guid.NewGuid()}.txt";
    
    // Act - Write
    await using (var writeStream = await service.CreateWriteStreamAsync(path))
    {
        await writeStream.WriteAsync(testData);
        await writeStream.CompleteAsync();
    }
    
    // Act - Read
    var readData = new MemoryStream();
    await using (var readStream = await service.OpenReadStreamAsync(path))
    {
        await readStream.CopyToAsync(readData);
    }
    
    // Assert
    Assert.Equal(testData, readData.ToArray());
}

[Fact]
public async Task MFS_LargeFile_StreamingWithoutMemorySpike()
{
    // Arrange
    var service = new IpfsFileSystemService(JSRuntime);
    var path = "/test/large-file.bin";
    var initialMemory = GC.GetTotalMemory(true);
    
    // Act - Stream 10MB file
    await using (var writeStream = await service.CreateWriteStreamAsync(path))
    {
        for (int i = 0; i < 40; i++) // 40 * 256KB = 10MB
        {
            var chunk = new byte[256 * 1024];
            await writeStream.WriteAsync(chunk);
            
            // Assert - Memory doesn't grow significantly
            var currentMemory = GC.GetTotalMemory(false);
            Assert.True(currentMemory - initialMemory < 3 * 1024 * 1024); // Max 3MB growth
        }
        await writeStream.CompleteAsync();
    }
}

[Fact]
public async Task MFS_DirectoryOperations_CreateParents()
{
    // Arrange
    var service = new IpfsFileSystemService(JSRuntime);
    var deepPath = "/documents/2024/november/reports/monthly.pdf";
    
    // Act
    await using (var stream = await service.CreateWriteStreamAsync(deepPath))
    {
        await stream.WriteAsync(new byte[] { 1, 2, 3 });
        await stream.CompleteAsync();
    }
    
    // Assert - Parent directories created
    Assert.True(await service.ExistsAsync("/documents"));
    Assert.True(await service.ExistsAsync("/documents/2024"));
    Assert.True(await service.ExistsAsync("/documents/2024/november"));
    Assert.True(await service.ExistsAsync("/documents/2024/november/reports"));
}
```

### Mobile Device Testing Checklist

#### iOS Safari (200MB RAM limit)
- [ ] Upload 50MB file without crashes
- [ ] Download 50MB file without crashes
- [ ] Multiple concurrent 10MB uploads (3 files)
- [ ] Background tab doesn't lose progress
- [ ] Orientation change during upload/download
- [ ] Address bar hide/show doesn't interrupt

#### Android Chrome Go (512MB RAM limit)
- [ ] Upload 100MB file successfully
- [ ] Download 100MB file successfully
- [ ] Low memory warning handling
- [ ] App switch and resume operation
- [ ] Network interruption recovery
- [ ] Progress indicator accuracy

#### Performance Metrics
- [ ] Memory usage stays under 3MB during operations
- [ ] Chunk processing < 100ms per 256KB
- [ ] UI remains responsive during transfers
- [ ] Progress updates at least every second
- [ ] No memory leaks after 10 operations

### JavaScript Module Tests

```javascript
// ipfs-module.test.js
describe('IPFS Stream Operations', () => {
    test('createWriteStream handles MFS paths', async () => {
        const stream = await createWriteStream('/test/file.txt');
        
        // Write chunks
        await stream.writeChunk(new Uint8Array([1, 2, 3]));
        await stream.writeChunk(new Uint8Array([4, 5, 6]));
        
        // Complete and verify CID returned
        const cid = await stream.complete();
        expect(cid).toMatch(/^Qm[a-zA-Z0-9]{44}$/);
        
        // Verify file exists in MFS
        const exists = await ipfs.files.stat('/test/file.txt');
        expect(exists).toBeDefined();
    });
    
    test('createReadStream handles both MFS and CID', async () => {
        // Test MFS path
        const mfsStream = await createReadStream('/test/file.txt');
        expect(mfsStream.size).toBeGreaterThan(0);
        
        // Test CID
        const cidStream = await createReadStream('QmXyz...');
        expect(cidStream.size).toBeGreaterThan(0);
    });
    
    test('memory usage remains constant', async () => {
        const initialMemory = performance.memory.usedJSHeapSize;
        const stream = await createWriteStream('/test/large.bin');
        
        // Write 10MB in chunks
        for (let i = 0; i < 40; i++) {
            const chunk = new Uint8Array(256 * 1024);
            await stream.writeChunk(chunk);
        }
        
        await stream.complete();
        const finalMemory = performance.memory.usedJSHeapSize;
        
        // Memory growth should be minimal (< 3MB)
        expect(finalMemory - initialMemory).toBeLessThan(3 * 1024 * 1024);
    });
});
```

## Verification Strategy

1. **Unit Testing**: Verify individual stream operations
2. **Integration Testing**: Test full upload/download cycles
3. **Performance Testing**: Monitor memory usage patterns
4. **Mobile Testing**: Manual verification on target devices
5. **Load Testing**: Multiple concurrent operations
6. **Error Recovery**: Network interruption scenarios

## Next Steps

The principal-engineer should:
1. Update the JavaScript module to support MFS operations
2. Implement the stream creation methods
3. Ensure proper path detection (MFS vs CID)
4. Handle MFS-specific operations (create parents, etc.)
5. Run complete test suite including mobile device testing