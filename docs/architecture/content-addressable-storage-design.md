# Content Addressable Storage (CAS) Architecture Design

## Executive Summary

This document outlines the design for a Content Addressable Storage (CAS) system that supports storing multiple data types, content-based addressing, and type-aware enumeration with filtering capabilities.

## Current State Analysis

### Existing Infrastructure
- **Backend**: .NET 9.0 service architecture
- **Storage**: EasyCaching with disk-based persistence  
- **Hashing**: SHA256 for key generation
- **Serialization**: System.Text.Json
- **No Frontend**: Currently backend-only, no browser components

### Key Finding: IndexedDB Incompatibility
**IndexedDB is a browser-based storage API and cannot be directly used in a .NET backend service.** This fundamental constraint requires us to propose alternative solutions.

## Proposed IContentAddressableStorage<T> Interface Design

### Core Interface
```csharp
public interface IContentAddressableStorage<T> where T : class
{
    // Store content and return its hash
    Task<string> StoreAsync(T entity, CancellationToken cancellation = default);
    
    // Retrieve content by hash
    Task<T?> GetAsync(string contentHash, CancellationToken cancellation = default);
    
    // Check existence
    Task<bool> ExistsAsync(string contentHash, CancellationToken cancellation = default);
    
    // Delete content
    Task<bool> DeleteAsync(string contentHash, CancellationToken cancellation = default);
    
    // Enumerate with filtering
    Task<IAsyncEnumerable<T>> EnumerateAsync(
        Expression<Func<T, bool>>? filter = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellation = default);
    
    // Count matching entities
    Task<long> CountAsync(
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellation = default);
}
```

### Multi-Type Storage Interface
```csharp
public interface ITypedContentAddressableStorage
{
    // Store any type
    Task<string> StoreAsync<T>(T entity, CancellationToken cancellation = default) 
        where T : class;
    
    // Get specific type
    Task<T?> GetAsync<T>(string contentHash, CancellationToken cancellation = default) 
        where T : class;
    
    // Enumerate by type with filtering
    Task<IAsyncEnumerable<T>> EnumerateByTypeAsync<T>(
        Expression<Func<T, bool>>? filter = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellation = default) 
        where T : class;
}
```

## Storage Backend Options

### Option 1: Enhanced File System Storage (Recommended for MVP)
**Approach**: Extend current disk-based storage with type partitioning

**Structure**:
```
cas-storage/
├── metadata.db         # SQLite for indexing
├── types/
│   ├── Image/
│   │   ├── 2f/
│   │   │   └── 2f4e5a6b7c8d9e1f...
│   ├── OcrResult/
│   │   ├── 3a/
│   │   │   └── 3a7b9c2d4e6f8a1b...
│   └── Receipt/
│       └── 4b/
│           └── 4b8c1d3e5f7a9b2c...
```

**Advantages**:
- Leverages existing disk storage patterns
- Simple implementation
- Type partitioning for efficient enumeration
- SQLite for metadata indexing and filtering

**Implementation**:
- Use SHA256 for content addressing
- Store type metadata in SQLite for fast queries
- Implement type-specific folders for organization
- Support LINQ expression translation to SQL

### Option 2: Hybrid SQL + File Storage
**Approach**: SQL database for metadata, file system for content

**Database Schema**:
```sql
CREATE TABLE CasEntries (
    ContentHash VARCHAR(64) PRIMARY KEY,
    TypeName VARCHAR(255) NOT NULL,
    StoredAt DATETIME NOT NULL,
    SizeInBytes BIGINT NOT NULL,
    Metadata NVARCHAR(MAX), -- JSON
    FilePath VARCHAR(500)
);

CREATE INDEX IX_TypeName ON CasEntries(TypeName);
CREATE INDEX IX_StoredAt ON CasEntries(StoredAt);
```

**Advantages**:
- Powerful querying via SQL
- Efficient filtering on metadata
- Maintains content/metadata separation
- Supports complex LINQ expressions

### Option 3: Document Database (MongoDB/CosmosDB)
**Approach**: Store both content and metadata in document database

**Document Structure**:
```json
{
  "_id": "2f4e5a6b7c8d9e1f...",
  "type": "Image",
  "storedAt": "2024-01-15T10:30:00Z",
  "sizeInBytes": 245678,
  "content": "base64_encoded_content",
  "metadata": {
    "processed": false,
    "source": "camera",
    "capturedAt": "2024-01-15T10:29:00Z"
  }
}
```

**Advantages**:
- Native support for different types
- Built-in indexing and querying
- Horizontal scalability
- Rich query capabilities

## IndexedDB Alternative: Browser-Based CAS

Since IndexedDB was mentioned but isn't applicable to the backend, here's how a browser-based CAS would work if a web frontend is added:

### Browser CAS Architecture
```javascript
// IndexedDB Schema
const db = await openDB('CAS', 1, {
  upgrade(db) {
    // Store for each type
    const imageStore = db.createObjectStore('images', { keyPath: 'hash' });
    imageStore.createIndex('processed', 'metadata.processed');
    
    const ocrStore = db.createObjectStore('ocrResults', { keyPath: 'hash' });
    ocrStore.createIndex('sourceImage', 'metadata.sourceImageHash');
  }
});
```

### Synchronization Strategy
If browser storage is needed:
1. **Backend CAS**: Primary source of truth
2. **Browser IndexedDB**: Local cache/offline storage
3. **Sync Protocol**: REST/WebSocket for bi-directional sync
4. **Conflict Resolution**: Last-write-wins or version vectors

## Recommended Architecture

### 1. Backend CAS Implementation
```
┌─────────────────────────────────────────┐
│         Application Layer               │
├─────────────────────────────────────────┤
│    IContentAddressableStorage<T>        │
│    ITypedContentAddressableStorage      │
├─────────────────────────────────────────┤
│      Storage Implementations            │
│  ┌─────────────┬──────────────────┐    │
│  │ FileSystem  │  SQLite Metadata │    │
│  │   Storage   │     Index         │    │
│  └─────────────┴──────────────────┘    │
├─────────────────────────────────────────┤
│         Physical Storage                │
│  ┌─────────────┬──────────────────┐    │
│  │   Files     │   metadata.db    │    │
│  └─────────────┴──────────────────┘    │
└─────────────────────────────────────────┘
```

### 2. Type Registration System
```csharp
public interface IContentTypeRegistry
{
    void Register<T>() where T : class;
    Type? GetType(string typeName);
    string GetTypeName(Type type);
}
```

### 3. Content Processing Pipeline
```
Image Upload → Hash Generation → Type Detection → 
Storage → Metadata Indexing → Processing Queue
```

## Implementation Phases

### Phase 1: Core CAS (Week 1-2)
- [ ] IContentAddressableStorage<T> interface
- [ ] File-based storage implementation
- [ ] SHA256 content addressing
- [ ] Basic type partitioning

### Phase 2: Metadata & Querying (Week 3-4)
- [ ] SQLite metadata store
- [ ] LINQ to SQL translation
- [ ] Filtering implementation
- [ ] Enumeration with pagination

### Phase 3: Multi-Type Support (Week 5-6)
- [ ] Type registry
- [ ] Generic storage operations
- [ ] Type-specific serialization
- [ ] Migration from existing cache

### Phase 4: Advanced Features (Week 7-8)
- [ ] Compression support
- [ ] Deduplication
- [ ] Garbage collection
- [ ] Performance optimization

## Design Principles Applied

### SOLID
- **S**: Each storage implementation has single responsibility
- **O**: Open for extension via interface implementation
- **L**: All implementations satisfy interface contract
- **I**: Separate interfaces for typed vs generic storage
- **D**: Depend on abstractions, not concrete implementations

### KISS
- Start with file system + SQLite (simplest viable solution)
- Add complexity only when needed
- Clear separation of concerns

### DRY
- Shared hash generation logic
- Common metadata structure
- Reusable filtering expressions

### YAGNI
- No premature optimization
- No browser sync until frontend exists
- No distributed storage initially

### TRIZ - Ideal Final Result
- Content automatically organizes itself (content-addressing)
- No manual categorization needed (type inference)
- Self-cleaning via content deduplication

## Migration Strategy

### From Current Cache to CAS
1. **Parallel Operation**: Run CAS alongside cache initially
2. **Gradual Migration**: Migrate cache entries to CAS
3. **Cutover**: Switch to CAS once stable
4. **Cleanup**: Remove old cache system

## Performance Considerations

### Expected Performance
- **Store**: O(1) hash computation + O(1) file write
- **Get**: O(1) hash lookup + O(1) file read
- **Enumerate**: O(n) with index, O(n²) without
- **Filter**: O(log n) with index, O(n) full scan

### Optimization Strategies
- Content deduplication via hashing
- Type-based partitioning
- Metadata indexing in SQLite
- Async I/O operations
- Connection pooling for database

## Security Considerations

- Content validation before storage
- Hash verification on retrieval
- Access control per type
- Audit logging for compliance
- Encryption at rest (optional)

## Conclusion

### Recommendation
Implement **Option 1 (Enhanced File System Storage)** as it:
1. Builds on existing infrastructure
2. Provides required functionality
3. Maintains simplicity
4. Allows future migration to other backends

### Why Not IndexedDB
IndexedDB is a **browser-only API** and cannot be used in .NET backend services. If browser storage is needed in the future, we recommend:
1. Building a web frontend with IndexedDB support
2. Implementing sync between backend CAS and browser storage
3. Using service workers for offline capability

### Next Steps
1. Review and approve this design
2. Create implementation tasks
3. Set up development environment
4. Begin Phase 1 implementation

## Appendix: Example Usage

### Storing an Image
```csharp
var imageStorage = serviceProvider.GetService<IContentAddressableStorage<ImageEntity>>();
var imageEntity = new ImageEntity { 
    Data = imageBytes, 
    Source = "camera",
    CapturedAt = DateTime.UtcNow 
};
string imageHash = await imageStorage.StoreAsync(imageEntity);
```

### Finding Unprocessed Images
```csharp
var unprocessedImages = await imageStorage.EnumerateAsync(
    filter: img => !img.Metadata.ContainsKey("ocrProcessed"),
    limit: 100
);

await foreach(var image in unprocessedImages)
{
    // Process image
    var ocrResult = await ProcessOcr(image);
    
    // Store OCR result
    var ocrStorage = serviceProvider.GetService<IContentAddressableStorage<OcrResult>>();
    await ocrStorage.StoreAsync(ocrResult);
    
    // Update image metadata
    image.Metadata["ocrProcessed"] = true;
    image.Metadata["ocrResultHash"] = ocrResult.Hash;
}
```