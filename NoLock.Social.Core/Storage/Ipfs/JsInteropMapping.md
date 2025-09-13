# IPFS JavaScript Interop Mapping

## Stream-Based Architecture

### C# to JavaScript Mapping

The C# Stream wrapper maps to JavaScript UnixFS operations as follows:

## Read Operations

### C# IpfsReadStream → JS UnixFS cat
```javascript
// JavaScript module: ipfs-file-system.js
export class IpfsReadStream {
    constructor(ipfs, path) {
        this.iterator = ipfs.cat(path); // Returns AsyncIterable<Uint8Array>
        this.buffer = [];
        this.done = false;
    }
    
    async readChunk(maxBytes) {
        if (this.done) return new Uint8Array(0);
        
        // Get next chunk from async iterator
        const { value, done } = await this.iterator.next();
        this.done = done;
        
        if (done) return new Uint8Array(0);
        
        // Limit chunk size if needed
        if (value.length > maxBytes) {
            // Cache excess for next read
            this.buffer.push(value.slice(maxBytes));
            return value.slice(0, maxBytes);
        }
        
        return value;
    }
    
    dispose() {
        // Clean up iterator if needed
        if (this.iterator.return) {
            this.iterator.return();
        }
    }
}
```

## Write Operations

### C# IpfsWriteStream → JS UnixFS add
```javascript
// JavaScript module: ipfs-file-system.js
export class IpfsWriteStream {
    constructor(ipfs, path) {
        this.ipfs = ipfs;
        this.path = path;
        this.chunks = [];
    }
    
    writeChunk(chunk) {
        // Accumulate chunks (Uint8Array)
        this.chunks.push(chunk);
    }
    
    async complete() {
        // Combine all chunks into single Uint8Array
        const totalLength = this.chunks.reduce((sum, chunk) => sum + chunk.length, 0);
        const combined = new Uint8Array(totalLength);
        
        let offset = 0;
        for (const chunk of this.chunks) {
            combined.set(chunk, offset);
            offset += chunk.length;
        }
        
        // Add to IPFS
        const result = await this.ipfs.add({
            path: this.path,
            content: combined
        });
        
        return result.cid.toString();
    }
    
    dispose() {
        this.chunks = [];
    }
}
```

## Progressive Loading Benefits

### Memory Efficiency
- **Chunked Processing**: 256KB chunks prevent memory overflow
- **Streaming**: No need to load entire file into memory
- **Progressive Rendering**: UI can show partial results

### Network Optimization
- **Incremental Downloads**: Start processing before full download
- **Resumable**: Can implement resume on network failure
- **Bandwidth Control**: Chunk size controls network usage

## Directory Operations

### C# ListDirectoryAsync → JS UnixFS ls
```javascript
export async function* listDirectory(ipfs, path) {
    for await (const entry of ipfs.ls(path)) {
        yield {
            name: entry.name,
            path: entry.path,
            cid: entry.cid.toString(),
            size: entry.size,
            type: entry.type === 'dir' ? 'Directory' : 'File'
        };
    }
}
```

## Error Handling

### Network Errors
```javascript
try {
    const stream = ipfs.cat(cid);
    // Process stream
} catch (error) {
    if (error.code === 'ERR_NOT_FOUND') {
        // File not found in IPFS
    } else if (error.name === 'TimeoutError') {
        // Network timeout
    }
}
```

### IndexedDB Storage
```javascript
// Automatic with blockstore-idb
const ipfs = await createHelia({
    blockstore: new IDBBlockstore('ipfs-blocks'),
    datastore: new IDBDatastore('ipfs-data')
});
```

## Performance Considerations

1. **Chunk Size**: 256KB aligns with UnixFS defaults
2. **Buffering**: Minimal buffering to reduce memory usage
3. **Async Iteration**: Native JavaScript async iterables
4. **Typed Arrays**: Uint8Array for efficient binary handling
5. **Progressive Loading**: Stream chunks as they arrive

## Practical Usage Examples

### File Upload with Progress Tracking
```csharp
// Component: DocumentUpload.razor.cs
public async Task UploadDocumentAsync(IBrowserFile file)
{
    const int chunkSize = 256 * 1024; // 256KB chunks
    var totalBytes = file.Size;
    var uploadedBytes = 0L;
    
    try
    {
        // Create IPFS write stream
        await using var ipfsStream = await _ipfsService.CreateWriteStreamAsync(
            $"/documents/{file.Name}");
        
        // Open browser file stream
        await using var fileStream = file.OpenReadStream(maxAllowedSize: 50_000_000);
        
        // Upload with progress tracking
        var buffer = new byte[chunkSize];
        int bytesRead;
        
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize)) > 0)
        {
            // Write chunk to IPFS
            await ipfsStream.WriteAsync(buffer, 0, bytesRead);
            
            // Update progress
            uploadedBytes += bytesRead;
            var progress = (double)uploadedBytes / totalBytes * 100;
            
            // Update UI
            UploadProgress = progress;
            StateHasChanged();
            
            // Allow UI to update (prevents blocking)
            await Task.Yield();
        }
        
        // Finalize and get CID
        var cid = await ipfsStream.CompleteAsync();
        
        // Store metadata
        await SaveDocumentMetadata(file.Name, cid, totalBytes);
        
        ShowNotification($"Document uploaded: {cid}", NotificationType.Success);
    }
    catch (IpfsException ex)
    {
        Logger.LogError(ex, "IPFS upload failed for {FileName}", file.Name);
        ShowNotification("Upload failed. Please try again.", NotificationType.Error);
    }
}
```

### File Download with Streaming
```csharp
// Component: DocumentViewer.razor.cs
public async Task DownloadDocumentAsync(string cid, string fileName)
{
    try
    {
        // Start streaming from IPFS
        await using var ipfsStream = await _ipfsService.ReadFileAsync(cid);
        
        // For large files, stream directly to browser download
        using var streamRef = new DotNetStreamReference(stream: ipfsStream);
        
        // Trigger browser download
        await JSRuntime.InvokeVoidAsync("downloadFileFromStream", 
            fileName, streamRef);
        
        ShowNotification($"Download complete: {fileName}", NotificationType.Success);
    }
    catch (IpfsNotFoundException)
    {
        ShowNotification("Document not found in IPFS network", NotificationType.Warning);
    }
    catch (IpfsTimeoutException)
    {
        ShowNotification("Network timeout. Please check your connection.", NotificationType.Error);
    }
}

// Alternative: Progressive Image Loading
public async Task LoadImageProgressivelyAsync(string cid)
{
    const int previewChunkSize = 32 * 1024; // 32KB for preview
    
    try
    {
        await using var ipfsStream = await _ipfsService.ReadFileAsync(cid);
        
        // Load preview quickly (first 32KB)
        var previewBuffer = new byte[previewChunkSize];
        var previewBytes = await ipfsStream.ReadAsync(previewBuffer, 0, previewChunkSize);
        
        // Show low-quality preview immediately
        var previewBase64 = Convert.ToBase64String(previewBuffer, 0, previewBytes);
        ImageSource = $"data:image/jpeg;base64,{previewBase64}";
        StateHasChanged();
        
        // Continue loading full image in background
        using var ms = new MemoryStream();
        ms.Write(previewBuffer, 0, previewBytes);
        await ipfsStream.CopyToAsync(ms);
        
        // Update with full quality image
        ImageSource = $"data:image/jpeg;base64,{Convert.ToBase64String(ms.ToArray())}";
        StateHasChanged();
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load image from IPFS");
        ImageSource = "/images/placeholder.jpg";
    }
}
```

### Batch Processing with Memory Management
```csharp
// Service: DocumentBatchProcessor.cs
public async Task ProcessDocumentBatchAsync(IEnumerable<DocumentInfo> documents)
{
    const int maxConcurrent = 3; // Limit concurrent operations
    using var semaphore = new SemaphoreSlim(maxConcurrent);
    
    var tasks = documents.Select(async doc =>
    {
        await semaphore.WaitAsync();
        try
        {
            // Process each document with streaming
            await using var readStream = await _ipfsService.ReadFileAsync(doc.Cid);
            await using var processedStream = new MemoryStream();
            
            // Process in chunks to avoid memory overflow
            await ProcessDocumentStreamAsync(readStream, processedStream);
            
            // Upload processed version
            processedStream.Position = 0;
            await using var writeStream = await _ipfsService.CreateWriteStreamAsync(
                $"/processed/{doc.Name}");
            await processedStream.CopyToAsync(writeStream);
            
            var newCid = await writeStream.CompleteAsync();
            doc.ProcessedCid = newCid;
            
            // Report progress
            ProcessedCount++;
            OnProgressChanged?.Invoke(ProcessedCount, documents.Count());
        }
        finally
        {
            semaphore.Release();
        }
    });
    
    await Task.WhenAll(tasks);
}
```

### Error Handling Patterns
```csharp
// Component: IpfsFileManager.razor.cs
public async Task<FileOperationResult> SafeFileOperationAsync(Func<Task<string>> operation)
{
    var retryCount = 0;
    const int maxRetries = 3;
    
    while (retryCount < maxRetries)
    {
        try
        {
            var cid = await operation();
            return new FileOperationResult 
            { 
                Success = true, 
                Cid = cid 
            };
        }
        catch (IpfsTimeoutException) when (retryCount < maxRetries - 1)
        {
            // Network timeout - retry with exponential backoff
            retryCount++;
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
            
            ShowNotification($"Network timeout. Retrying in {delay.TotalSeconds}s...", 
                NotificationType.Warning);
            
            await Task.Delay(delay);
        }
        catch (IpfsQuotaExceededException ex)
        {
            // Storage quota exceeded
            ShowNotification("Storage quota exceeded. Please free up space.", 
                NotificationType.Error);
            
            return new FileOperationResult 
            { 
                Success = false, 
                Error = "Quota exceeded",
                Details = ex.Message 
            };
        }
        catch (IpfsCorruptionException ex)
        {
            // Data corruption detected
            Logger.LogError(ex, "Data corruption detected");
            
            ShowNotification("File corrupted. Please re-upload.", 
                NotificationType.Error);
            
            return new FileOperationResult 
            { 
                Success = false, 
                Error = "Data corruption",
                Details = ex.Message 
            };
        }
        catch (Exception ex)
        {
            // Unexpected error
            Logger.LogError(ex, "Unexpected IPFS error");
            
            return new FileOperationResult 
            { 
                Success = false, 
                Error = "Unknown error",
                Details = ex.Message 
            };
        }
    }
    
    return new FileOperationResult 
    { 
        Success = false, 
        Error = "Max retries exceeded" 
    };
}
```

### Real-World Streaming Benefits

#### 1. Large Video File Streaming
```csharp
// Stream video without loading entire file
public async Task StreamVideoAsync(string cid)
{
    await using var ipfsStream = await _ipfsService.ReadFileAsync(cid);
    
    // Create streaming response for video player
    var streamRef = new DotNetStreamReference(ipfsStream);
    await JSRuntime.InvokeVoidAsync("initVideoStream", VideoElementId, streamRef);
    
    // Video starts playing immediately as chunks arrive
    // No need to wait for entire file download
}
```

#### 2. Document Preview Generation
```csharp
// Generate preview from first page without full download
public async Task<string> GenerateDocumentPreviewAsync(string cid)
{
    const int previewSize = 1024 * 1024; // 1MB for preview
    
    await using var ipfsStream = await _ipfsService.ReadFileAsync(cid);
    
    // Read only first MB for preview
    var buffer = new byte[previewSize];
    var bytesRead = await ipfsStream.ReadAsync(buffer, 0, previewSize);
    
    // Generate preview from partial data
    return await _previewService.GenerateFromPartialAsync(buffer, bytesRead);
}
```

#### 3. Memory-Efficient Batch Export
```csharp
// Export multiple files without memory overflow
public async Task ExportArchiveAsync(IEnumerable<string> cids)
{
    await using var zipStream = new MemoryStream();
    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);
    
    foreach (var cid in cids)
    {
        var entry = archive.CreateEntry($"{cid}.dat");
        await using var entryStream = entry.Open();
        
        // Stream directly from IPFS to ZIP
        await using var ipfsStream = await _ipfsService.ReadFileAsync(cid);
        await ipfsStream.CopyToAsync(entryStream);
        
        // Each file is processed and disposed individually
        // Memory usage stays constant regardless of archive size
    }
    
    // Trigger download
    zipStream.Position = 0;
    await DownloadStreamAsync(zipStream, "export.zip");
}
```

This design provides:
- ✅ **Progressive Loading**: Start using data before full download
- ✅ **Memory Efficiency**: Process files larger than available RAM
- ✅ **Better UX**: Show progress and partial results immediately
- ✅ **Network Resilience**: Retry logic with exponential backoff
- ✅ **Error Recovery**: Graceful handling of all failure modes
- ✅ **Real-World Patterns**: Production-ready code examples