# IPFS Helia Integration - Architecture Diagrams

## 1. Component Architecture
Shows how C# Stream components connect to Helia UnixFS through JavaScript interop.

```mermaid
graph TB
    subgraph "Blazor Layer (C#)"
        A[IIpfsFileSystem Interface]
        B[IpfsReadStream]
        C[IpfsWriteStream]
        D[IpfsFileInfo]
    end
    
    subgraph "JavaScript Interop"
        E[ipfsFileSystem.js Module]
        F[Stream Chunk Handler]
    end
    
    subgraph "Helia Layer"
        G[Helia Instance]
        H[UnixFS API]
        I[Blockstore<br/>IndexedDB]
    end
    
    A --> B
    A --> C
    A --> D
    B --> E
    C --> E
    E --> F
    F --> G
    G --> H
    H --> I
    
    style A fill:#e1f5e1
    style E fill:#fff3e0
    style G fill:#e3f2fd
```

**Key Points:**
- **IIpfsFileSystem**: Primary C# interface exposing Stream-based operations
- **Stream Wrappers**: IpfsReadStream and IpfsWriteStream handle chunked data transfer
- **JavaScript Module**: Thin translation layer, no business logic
- **Helia UnixFS**: Native async iterable support for streaming

---

## 2. Data Flow Diagram
Illustrates how data streams from Blazor application to IPFS storage in 256KB chunks.

```mermaid
sequenceDiagram
    participant App as Blazor App
    participant Stream as IpfsWriteStream
    participant JS as JS Interop
    participant Helia as Helia UnixFS
    participant Store as IndexedDB
    
    App->>Stream: Write(byte[], 0, length)
    Stream->>Stream: Buffer to 256KB
    
    loop For each 256KB chunk
        Stream->>JS: InvokeAsync("writeChunk", chunk)
        JS->>Helia: yield Uint8Array
        Helia->>Helia: Create DAG node
        Helia->>Store: Store block
        Store-->>Helia: Block CID
        Helia-->>JS: Progress update
        JS-->>Stream: Bytes written
    end
    
    Stream->>JS: InvokeAsync("finalize")
    JS->>Helia: Complete UnixFS file
    Helia-->>JS: File CID
    JS-->>Stream: Final CID
    Stream-->>App: Return CID
```

**Key Points:**
- **256KB Chunks**: Optimal size for browser memory and IPFS blocks
- **Async Streaming**: Non-blocking data transfer using async iterables
- **Progressive Updates**: Real-time progress reporting during upload
- **CID Generation**: Content-addressed storage with unique identifiers

---

## 3. Storage Architecture
Shows how IPFS data persists in browser's IndexedDB for offline capability.

```mermaid
graph LR
    subgraph "Browser Storage"
        A[IndexedDB]
        B[(Blocks Table)]
        C[(Pins Table)]
        D[(Metadata Table)]
    end
    
    subgraph "Helia Blockstore"
        E[IDBBlockstore]
        F[Block Cache]
        G[Pin Manager]
    end
    
    subgraph "IPFS Data"
        H[File Blocks<br/>256KB each]
        I[DAG Nodes]
        J[Root CIDs]
    end
    
    E --> A
    E --> B
    G --> C
    F --> D
    
    H --> E
    I --> E
    J --> G
    
    style A fill:#fff9c4
    style E fill:#f3e5f5
    style H fill:#e8f5e9
```

**Key Points:**
- **IndexedDB**: Persistent browser storage (50% of available disk)
- **Block-level Storage**: Each 256KB chunk stored separately
- **Pin Management**: Prevents garbage collection of important files
- **Offline Support**: Full functionality without network connectivity

---

## Summary

These diagrams illustrate the minimal abstraction approach:

1. **Component Architecture**: Direct mapping from C# Streams to Helia UnixFS
2. **Data Flow**: Efficient chunked streaming with progress updates
3. **Storage Architecture**: Browser-native persistence using IndexedDB

The design follows "programming by intent" - developers work with familiar C# Stream patterns while Helia handles the IPFS complexity underneath.

**Total Abstraction Layers**: 2
- C# Stream interface (familiar API)
- JavaScript translation (thin mapping layer)

This architecture ensures junior developers can work with standard .NET patterns while leveraging the full power of IPFS in the browser.