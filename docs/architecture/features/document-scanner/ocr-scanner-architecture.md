# Document Scanner and OCR Feature Architecture with Pluggable Processor System

## Executive Summary

This document outlines the architecture for implementing a pluggable document scanning and OCR (Optical Character Recognition) system in the NoLock.Social platform. The architecture features a plugin-based design that enables seamless addition of new document types (receipts, checks, W4, W2, 1099, and future tax forms) without modifying the core system. Each document type is handled by a dedicated processor plugin that implements a common interface while providing document-specific logic for validation, parsing, and data extraction. The implementation follows SOLID principles and clean architecture patterns to ensure extensibility, maintainability, and scalability.

## Table of Contents

1. [Overview](#overview)
2. [Architecture Overview](#architecture-overview)
3. [Component Architecture](#component-architecture)
4. [API Integration Design](#api-integration-design)
5. [UI/UX Design](#uiux-design)
6. [Project Structure](#project-structure)
7. [Technology Stack](#technology-stack)
8. [Implementation Details](#implementation-details)
9. [Security and Privacy](#security-and-privacy)
10. [Testing Strategy](#testing-strategy)
11. [Performance Considerations](#performance-considerations)
12. [Implementation Roadmap](#implementation-roadmap)

## Overview

### Business Requirements
- Capture documents using device cameras (mobile and desktop)
- Support multiple document types (receipts, checks, general documents)
- Process captured images through OCR service with async polling (1-2 minute delays)
- Display extracted data in a user-friendly format
- Support offline capture with deferred processing
- Ensure cross-device compatibility
- Store documents in Content-Addressable Storage (CAS)
- Provide browsable gallery for stored documents and OCR results

### Technical Requirements
- Integration with external OCR service at `https://nolock-ocr-services-qbhx5.ondigitalocean.app`
- Asynchronous OCR processing with polling mechanism (up to 2 minutes)
- Cryptographically signed document structures
- Content-Addressable Storage (CAS) integration
- Camera access via browser MediaDevices API
- Responsive design for mobile and desktop
- Component isolation in separate project (NoLock.Social.DocumentScanner)
- Auto-generated proxy using NSwag or OpenAPI Generator
- Error handling and retry logic
- Background job processing for OCR status polling

## Architecture Overview

### High-Level Architecture Diagram

```mermaid
graph TB
    subgraph "Blazor WebAssembly Client"
        UI[Scanner UI Component]
        CAM[Camera Service]
        IMG[Image Processor]
        STATE[State Manager]
        PROXY[OCR Service Proxy]
        SIGN[Document Signer]
        POLL[Polling Service]
        GALLERY[Gallery Component]
    end
    
    subgraph "Browser APIs"
        MEDIA[MediaDevices API]
        STORE[IndexedDB Storage]
    end
    
    subgraph "Storage Layer"
        CAS[Content-Addressable Storage]
        DOCS[Signed Documents]
    end
    
    subgraph "External Services"
        OCR[OCR Service API<br/>1-2 min processing]
    end
    
    UI --> CAM
    UI --> GALLERY
    CAM --> MEDIA
    CAM --> IMG
    IMG --> SIGN
    SIGN --> CAS
    SIGN --> DOCS
    STATE --> POLL
    POLL --> PROXY
    PROXY --> OCR
    POLL --> CAS
    STATE --> STORE
    GALLERY --> CAS
    GALLERY --> DOCS
    
    style UI fill:#f9f,stroke:#333,stroke-width:2px
    style OCR fill:#9ff,stroke:#333,stroke-width:2px
    style CAS fill:#ff9,stroke:#333,stroke-width:2px
    style SIGN fill:#9f9,stroke:#333,stroke-width:2px
```

### Component Hierarchy

```mermaid
graph TD
    ROOT[DocumentScannerComponent]
    ROOT --> CAMERA[CameraViewComponent]
    ROOT --> GALLERY[DocumentGalleryComponent]
    ROOT --> PREVIEW[DocumentPreviewComponent]
    ROOT --> STATUS[ProcessingStatusComponent]
    ROOT --> CONTROLS[ScannerControlsComponent]
    
    CAMERA --> VIEWFINDER[ViewfinderOverlay]
    CAMERA --> CAPTURE[CaptureButton]
    
    GALLERY --> GRID[DocumentGrid]
    GALLERY --> FILTER[FilterControls]
    GALLERY --> THUMB[ThumbnailView]
    
    PREVIEW --> IMAGE[ImageViewer]
    PREVIEW --> OCR[OCRTextViewer]
    PREVIEW --> META[DocumentMetadata]
    
    STATUS --> PROGRESS[PollingProgress]
    STATUS --> RETRY[RetryControls]
    
    CONTROLS --> MODE[ModeSelector]
    CONTROLS --> ACTIONS[ActionButtons]
```

## Component Architecture

### Core Components

#### 1. DocumentScannerComponent (Root Container)
```csharp
namespace NoLock.Social.DocumentScanner.Components
{
    public partial class DocumentScannerComponent : ComponentBase
    {
        // Orchestrates the scanning workflow
        // Manages state transitions
        // Handles error boundaries
    }
}
```

**Responsibilities:**
- Workflow orchestration
- State management coordination
- Error boundary handling
- Permission management

**State Management:**
```csharp
public enum ScannerState
{
    Idle,
    RequestingPermission,
    CameraActive,
    ImageCaptured,
    Processing,
    ResultsReady,
    Error
}
```

#### 2. CameraViewComponent
**Responsibilities:**
- Camera stream initialization
- Video element management
- Frame capture
- Device selection (front/back camera)

**Key Features:**
- Real-time camera preview
- Multiple camera support
- Torch/flash control (where available)
- Auto-focus and exposure adjustment

#### 3. DocumentGalleryComponent
**Responsibilities:**
- Display grid of stored documents
- Thumbnail generation from CAS
- Filter by document type and date
- Search by OCR content
- Batch operations support

**Key Features:**
- Lazy loading for performance
- Virtual scrolling for large collections
- Progressive image loading
- Quick preview on hover

#### 4. DocumentPreviewComponent
**Responsibilities:**
- Display full-size image from CAS
- Show OCR text results
- Display document metadata
- Signature verification status
- Export functionality

**Key Features:**
- Zoom and pan for images
- Text search in OCR results
- Copy text functionality
- Download original files

#### 5. ProcessingStatusComponent
**Responsibilities:**
- Show OCR processing progress
- Display polling status
- Handle retry operations
- Show error states
- Timeout notifications

### Service Layer Architecture

```mermaid
classDiagram
    class IScannerService {
        <<interface>>
        +InitializeCamera()
        +CaptureImage()
        +ProcessImage()
        +GetResults()
    }
    
    class ICameraService {
        <<interface>>
        +RequestPermission()
        +GetDevices()
        +StartStream()
        +StopStream()
        +CaptureFrame()
    }
    
    class IImageProcessingService {
        <<interface>>
        +PreprocessImage()
        +CropImage()
        +EnhanceQuality()
        +ConvertToBase64()
    }
    
    class IOCRProxyService {
        <<interface>>
        +ProcessReceipt()
        +ProcessCheck()
        +GetStatus()
    }
    
    class ScannerService {
        -ICameraService camera
        -IImageProcessingService processor
        -IOCRProxyService ocr
    }
    
    ScannerService ..|> IScannerService
    ScannerService --> ICameraService
    ScannerService --> IImageProcessingService
    ScannerService --> IOCRProxyService
```

### State Management Design

```csharp
public class ScannerStateManager
{
    private readonly IState<ScannerState> _state;
    private readonly IState<CapturedImage> _capturedImage;
    private readonly IState<OCRResult> _ocrResult;
    private readonly IState<ProcessingQueue> _processingQueue;
    
    public IObservable<ScannerState> StateChanges => _state.Changes;
    public IObservable<CapturedImage> ImageChanges => _capturedImage.Changes;
    public IObservable<OCRResult> ResultChanges => _ocrResult.Changes;
    public IObservable<ProcessingQueue> QueueChanges => _processingQueue.Changes;
}
```

## Cryptographic Document Architecture

### Signed Document Structure

The system implements a cryptographically signed document structure that ensures integrity and authenticity of both images and OCR results. This structure allows for progressive enhancement where images are immediately available while OCR processing occurs asynchronously.

```mermaid
graph TB
    subgraph "Signed Document v1.0"
        DOC[Document Container]
        DOC --> VER[Version: 1.0]
        DOC --> ID[Document ID: UUID]
        DOC --> TS[Timestamp: ISO8601]
        DOC --> IMG_HASH[Image Hash: SHA-256]
        DOC --> OCR_HASH[OCR Hash: SHA-256 or null]
        DOC --> META[Metadata]
        DOC --> SIG[Digital Signature: Ed25519]
    end
    
    subgraph "CAS Storage"
        IMG_HASH --> IMG_CAS[Image Blob in CAS]
        OCR_HASH --> OCR_CAS[OCR Result in CAS]
    end
    
    subgraph "Verification"
        SIG --> VERIFY[Signature Verification]
        VERIFY --> PUB_KEY[Public Key]
    end
```

### Document Schema Definition

```csharp
public class SignedDocument
{
    public string Version { get; set; } = "1.0";
    public Guid DocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Content References (CAS hashes)
    public string ImageHash { get; set; }  // SHA-256 of image content
    public string? OcrHash { get; set; }   // SHA-256 of OCR result (null initially)
    
    // Document Metadata
    public DocumentMetadata Metadata { get; set; }
    
    // Processing State
    public ProcessingState State { get; set; }
    
    // Cryptographic Signature
    public byte[] Signature { get; set; }  // Ed25519 signature
    public string SignerPublicKey { get; set; }
}

public class DocumentMetadata
{
    public DocumentType Type { get; set; }
    public string OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string MimeType { get; set; }
    public Dictionary<string, string> CustomFields { get; set; }
}

public enum ProcessingState
{
    ImageCaptured,      // Image saved, OCR not started
    OcrPending,         // OCR request submitted
    OcrProcessing,      // OCR in progress (polling)
    OcrCompleted,       // OCR results available
    OcrFailed,          // OCR processing failed
    OcrTimeout          // OCR exceeded 2-minute timeout
}
```

### Cryptographic Signing Process

```mermaid
sequenceDiagram
    participant User
    participant Camera
    participant Signer
    participant CAS
    participant DocStore
    
    User->>Camera: Capture Image
    Camera->>Signer: Raw Image Data
    
    Note over Signer: Generate SHA-256 hash
    Signer->>CAS: Store Image by Hash
    CAS-->>Signer: Confirm Storage
    
    Note over Signer: Create Document Structure
    Note over Signer: Set OcrHash = null
    Note over Signer: Sign with Ed25519
    
    Signer->>DocStore: Store Signed Document
    DocStore-->>User: Document ID
    
    Note over User: Image immediately available
```

### Document Update Process (After OCR)

```mermaid
sequenceDiagram
    participant Poller
    participant OCR
    participant Signer
    participant CAS
    participant DocStore
    
    Poller->>OCR: Check Status
    OCR-->>Poller: Results Ready
    
    Poller->>Signer: OCR Results
    Note over Signer: Generate SHA-256 hash
    Signer->>CAS: Store OCR by Hash
    
    Signer->>DocStore: Fetch Original Document
    DocStore-->>Signer: Original Document
    
    Note over Signer: Update OcrHash field
    Note over Signer: Update ProcessingState
    Note over Signer: Re-sign Document
    
    Signer->>DocStore: Update Signed Document
    DocStore-->>Poller: Update Confirmed
```

## Asynchronous OCR Processing

### Polling Architecture

The system implements a sophisticated polling mechanism to handle the 1-2 minute OCR processing delays while preventing mobile devices from sleeping.

```mermaid
graph TB
    subgraph "Async Processing Flow"
        CAPTURE[Image Capture]
        SUBMIT[Submit to OCR]
        QUEUE[Processing Queue]
        POLL[Polling Service]
        CHECK[Status Check]
        RESULT[OCR Result]
    end
    
    subgraph "Timing Strategy"
        T1[Initial: 5 sec]
        T2[Then: 10 sec]
        T3[Then: 15 sec]
        T4[Max: 30 sec intervals]
        TIMEOUT[Timeout: 2 min]
    end
    
    CAPTURE --> SUBMIT
    SUBMIT --> QUEUE
    QUEUE --> POLL
    POLL --> CHECK
    CHECK -->|Pending| POLL
    CHECK -->|Complete| RESULT
    
    POLL --> T1
    T1 --> T2
    T2 --> T3
    T3 --> T4
    T4 --> TIMEOUT
```

### Polling Service Implementation

```csharp
public interface IPollingService
{
    Task<Guid> SubmitForProcessingAsync(SignedDocument document, byte[] imageData);
    Task<OcrResult> PollForResultAsync(Guid taskId, CancellationToken cancellationToken);
    void CancelPolling(Guid taskId);
}

public class AdaptivePollingService : IPollingService
{
    private readonly IOCRServiceProxy _ocrProxy;
    private readonly IDocumentSigner _signer;
    private readonly ICASStorage _casStorage;
    private readonly ILogger<AdaptivePollingService> _logger;
    
    // Adaptive polling intervals (milliseconds)
    private readonly int[] _pollingIntervals = { 5000, 10000, 15000, 30000 };
    private const int MaxPollingDuration = 120000; // 2 minutes
    
    public async Task<OcrResult> PollForResultAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var intervalIndex = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check timeout
            if ((DateTime.UtcNow - startTime).TotalMilliseconds > MaxPollingDuration)
            {
                throw new OcrTimeoutException($"OCR processing exceeded {MaxPollingDuration}ms timeout");
            }
            
            // Check OCR status
            var status = await _ocrProxy.GetStatusAsync(taskId);
            
            switch (status.State)
            {
                case OcrState.Completed:
                    return await ProcessCompletedOcr(taskId, status.ResultId);
                    
                case OcrState.Failed:
                    throw new OcrProcessingException(status.ErrorMessage);
                    
                case OcrState.Processing:
                case OcrState.Queued:
                    // Continue polling with adaptive interval
                    var delay = _pollingIntervals[Math.Min(intervalIndex++, _pollingIntervals.Length - 1)];
                    await Task.Delay(delay, cancellationToken);
                    break;
            }
        }
        
        throw new OperationCanceledException("Polling was cancelled");
    }
    
    private async Task<OcrResult> ProcessCompletedOcr(Guid taskId, string resultId)
    {
        // Fetch OCR results
        var ocrData = await _ocrProxy.GetResultAsync(resultId);
        
        // Store in CAS
        var ocrHash = await _casStorage.StoreAsync(ocrData);
        
        // Update signed document
        await UpdateDocumentWithOcr(taskId, ocrHash);
        
        return ocrData;
    }
}
```

### Background Job Processing

```csharp
public class OcrBackgroundProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OcrBackgroundProcessor> _logger;
    private readonly Channel<OcrProcessingJob> _jobQueue;
    private readonly IJSRuntime _jsRuntime;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var pollingService = scope.ServiceProvider.GetRequiredService<IPollingService>();
                
                // Prevent mobile sleep during processing
                await AcquireWakeLock(job.DocumentId);
                
                // Process with individual timeout
                using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                jobCts.CancelAfter(TimeSpan.FromMinutes(2.5)); // Allow extra time for retries
                
                var result = await pollingService.PollForResultAsync(job.TaskId, jobCts.Token);
                
                // Notify completion
                await NotifyCompletion(job.DocumentId, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process OCR job {JobId}", job.TaskId);
                await NotifyFailure(job.DocumentId, ex.Message);
            }
            finally
            {
                // Release wake lock
                await ReleaseWakeLock(job.DocumentId);
            }
        }
    }
    
    private async Task AcquireWakeLock(Guid documentId)
    {
        try
        {
            // Use Wake Lock API to prevent sleep on mobile
            await _jsRuntime.InvokeVoidAsync("wakeLock.request", documentId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire wake lock");
        }
    }
    
    private async Task ReleaseWakeLock(Guid documentId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("wakeLock.release", documentId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release wake lock");
        }
    }
    
    public async Task EnqueueJobAsync(OcrProcessingJob job)
    {
        await _jobQueue.Writer.WriteAsync(job);
        _logger.LogInformation("Enqueued OCR job {JobId} for document {DocumentId}", 
            job.TaskId, job.DocumentId);
    }
}

// JavaScript Wake Lock Implementation
export class WakeLockManager {
    constructor() {
        this.locks = new Map();
    }
    
    async request(lockId) {
        if ('wakeLock' in navigator) {
            try {
                const wakeLock = await navigator.wakeLock.request('screen');
                this.locks.set(lockId, wakeLock);
                
                // Re-acquire on visibility change
                document.addEventListener('visibilitychange', async () => {
                    if (document.visibilityState === 'visible' && this.locks.has(lockId)) {
                        await this.request(lockId);
                    }
                });
                
                return true;
            } catch (err) {
                console.error(`Wake Lock error: ${err.message}`);
                return false;
            }
        }
        return false;
    }
    
    async release(lockId) {
        const lock = this.locks.get(lockId);
        if (lock) {
            await lock.release();
            this.locks.delete(lockId);
        }
    }
}
```

## Document Signing Implementation

### Ed25519 Signature Service

```csharp
public interface IDocumentSigner
{
    Task<SignedDocument> SignDocumentAsync(DocumentContent content);
    Task<bool> VerifyDocumentAsync(SignedDocument document);
    Task<SignedDocument> UpdateAndResignAsync(SignedDocument existing, Action<SignedDocument> update);
}

public class Ed25519DocumentSigner : IDocumentSigner
{
    private readonly IKeyManager _keyManager;
    private readonly ILogger<Ed25519DocumentSigner> _logger;
    
    public async Task<SignedDocument> SignDocumentAsync(DocumentContent content)
    {
        var document = new SignedDocument
        {
            Version = "1.0",
            DocumentId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ImageHash = content.ImageHash,
            OcrHash = content.OcrHash,
            Metadata = content.Metadata,
            State = ProcessingState.ImageCaptured
        };
        
        // Get signing key
        var keyPair = await _keyManager.GetSigningKeyPairAsync();
        document.SignerPublicKey = Convert.ToBase64String(keyPair.PublicKey);
        
        // Create canonical representation for signing
        var canonicalData = CreateCanonicalRepresentation(document);
        
        // Sign with Ed25519
        document.Signature = Ed25519.Sign(canonicalData, keyPair.PrivateKey);
        
        return document;
    }
    
    public async Task<bool> VerifyDocumentAsync(SignedDocument document)
    {
        try
        {
            // Extract public key
            var publicKey = Convert.FromBase64String(document.SignerPublicKey);
            
            // Create canonical representation
            var signature = document.Signature;
            document.Signature = null; // Remove signature for verification
            var canonicalData = CreateCanonicalRepresentation(document);
            document.Signature = signature; // Restore
            
            // Verify signature
            return Ed25519.Verify(signature, canonicalData, publicKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signature verification failed");
            return false;
        }
    }
    
    private byte[] CreateCanonicalRepresentation(SignedDocument doc)
    {
        // Create deterministic JSON representation
        var json = JsonSerializer.Serialize(new
        {
            version = doc.Version,
            documentId = doc.DocumentId,
            createdAt = doc.CreatedAt.ToUniversalTime().ToString("O"),
            updatedAt = doc.UpdatedAt?.ToUniversalTime().ToString("O"),
            imageHash = doc.ImageHash,
            ocrHash = doc.OcrHash,
            metadata = doc.Metadata,
            state = doc.State.ToString()
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        });
        
        return Encoding.UTF8.GetBytes(json);
    }
}
```

### Key Management

```csharp
public interface IKeyManager
{
    Task<Ed25519KeyPair> GetSigningKeyPairAsync();
    Task<Ed25519KeyPair> GenerateNewKeyPairAsync();
    Task RotateKeysAsync();
}

public class SecureKeyManager : IKeyManager
{
    private readonly ISecureStorage _secureStorage;
    private readonly ILogger<SecureKeyManager> _logger;
    private Ed25519KeyPair? _cachedKeyPair;
    
    public async Task<Ed25519KeyPair> GetSigningKeyPairAsync()
    {
        if (_cachedKeyPair != null)
            return _cachedKeyPair;
        
        // Try to load existing key
        var storedKey = await _secureStorage.GetAsync("signing_key");
        if (storedKey != null)
        {
            _cachedKeyPair = DeserializeKeyPair(storedKey);
            return _cachedKeyPair;
        }
        
        // Generate new key pair if none exists
        _cachedKeyPair = await GenerateNewKeyPairAsync();
        return _cachedKeyPair;
    }
    
    public async Task<Ed25519KeyPair> GenerateNewKeyPairAsync()
    {
        // Generate Ed25519 key pair
        var keyPair = Ed25519.GenerateKeyPair();
        
        // Store securely
        await _secureStorage.SetAsync("signing_key", SerializeKeyPair(keyPair));
        
        _logger.LogInformation("Generated new Ed25519 key pair");
        return keyPair;
    }
}
```

## Gallery Component Implementation

### Document Gallery Service

```csharp
public interface IDocumentGalleryService
{
    Task<PagedResult<DocumentSummary>> GetDocumentsAsync(
        int page, int pageSize, DocumentFilter filter);
    Task<byte[]> GenerateThumbnailAsync(string imageHash);
    Task<DocumentDetails> GetDocumentDetailsAsync(Guid documentId);
    Task DeleteDocumentAsync(Guid documentId);
}

public class DocumentGalleryService : IDocumentGalleryService
{
    private readonly ICASStorage _casStorage;
    private readonly IDocumentRepository _documentRepo;
    private readonly IThumbnailGenerator _thumbnailGen;
    private readonly IDocumentSigner _signer;
    
    public async Task<PagedResult<DocumentSummary>> GetDocumentsAsync(
        int page, int pageSize, DocumentFilter filter)
    {
        var query = _documentRepo.Query();
        
        // Apply filters
        if (filter.DocumentType.HasValue)
            query = query.Where(d => d.Metadata.Type == filter.DocumentType);
        
        if (filter.ProcessingState.HasValue)
            query = query.Where(d => d.State == filter.ProcessingState);
        
        if (!string.IsNullOrEmpty(filter.SearchText))
        {
            // Search in OCR content if available
            query = query.Where(d => d.OcrHash != null && 
                _casStorage.SearchContent(d.OcrHash, filter.SearchText));
        }
        
        // Sort by creation date descending
        query = query.OrderByDescending(d => d.CreatedAt);
        
        // Apply pagination
        var total = await query.CountAsync();
        var documents = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentSummary
            {
                DocumentId = d.DocumentId,
                ThumbnailUrl = GenerateThumbnailUrl(d.ImageHash),
                DocumentType = d.Metadata.Type,
                ProcessingState = d.State,
                CreatedAt = d.CreatedAt,
                HasOcr = d.OcrHash != null
            })
            .ToListAsync();
        
        return new PagedResult<DocumentSummary>
        {
            Items = documents,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
    
    public async Task<byte[]> GenerateThumbnailAsync(string imageHash)
    {
        // Check thumbnail cache
        var thumbnailHash = $"thumb_{imageHash}";
        if (await _casStorage.ExistsAsync(thumbnailHash))
        {
            return await _casStorage.RetrieveAsync(thumbnailHash);
        }
        
        // Generate thumbnail
        var imageData = await _casStorage.RetrieveAsync(imageHash);
        var thumbnail = await _thumbnailGen.GenerateAsync(imageData, 200, 200);
        
        // Store in CAS for future use
        await _casStorage.StoreAsync(thumbnail, thumbnailHash);
        
        return thumbnail;
    }
}
```

### Virtual Scrolling Implementation

```csharp
@page "/gallery"
@implements IAsyncDisposable
@inject IDocumentGalleryService GalleryService
@inject IJSRuntime JSRuntime

<div class="document-gallery" @ref="galleryContainer">
    <div class="gallery-header">
        <DocumentFilterControls @bind-Filter="currentFilter" OnFilterChanged="OnFilterChanged" />
    </div>
    
    <div class="gallery-grid" @onscroll="OnScroll">
        <Virtualize Items="@documents" Context="doc" ItemSize="250" OverscanCount="3">
            <DocumentThumbnail Document="doc" OnClick="() => OpenDocument(doc)" />
        </Virtualize>
    </div>
    
    @if (isLoading)
    {
        <div class="loading-indicator">
            <span>Loading documents...</span>
        </div>
    }
</div>

@code {
    private ElementReference galleryContainer;
    private List<DocumentSummary> documents = new();
    private DocumentFilter currentFilter = new();
    private bool isLoading = false;
    private int currentPage = 1;
    private bool hasMorePages = true;
    private CancellationTokenSource? loadingCts;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadDocuments();
        
        // Set up infinite scroll
        await JSRuntime.InvokeVoidAsync("gallery.setupInfiniteScroll", 
            galleryContainer, DotNetObjectReference.Create(this));
    }
    
    private async Task LoadDocuments(bool append = false)
    {
        if (isLoading || !hasMorePages) return;
        
        isLoading = true;
        loadingCts?.Cancel();
        loadingCts = new CancellationTokenSource();
        
        try
        {
            var result = await GalleryService.GetDocumentsAsync(
                currentPage, 20, currentFilter);
            
            if (append)
            {
                documents.AddRange(result.Items);
            }
            else
            {
                documents = result.Items.ToList();
            }
            
            hasMorePages = result.HasNextPage;
            currentPage++;
            
            StateHasChanged();
        }
        catch (OperationCanceledException)
        {
            // Cancelled - ignore
        }
        finally
        {
            isLoading = false;
        }
    }
    
    [JSInvokable]
    public async Task LoadMoreDocuments()
    {
        await LoadDocuments(append: true);
    }
    
    private async Task OnFilterChanged()
    {
        currentPage = 1;
        hasMorePages = true;
        await LoadDocuments(append: false);
    }
    
    private void OpenDocument(DocumentSummary doc)
    {
        NavigationManager.NavigateTo($"/document/{doc.DocumentId}");
    }
    
    public async ValueTask DisposeAsync()
    {
        loadingCts?.Cancel();
        loadingCts?.Dispose();
    }
}
```

### Preview Component with CAS Integration

```csharp
@page "/document/{DocumentId:guid}"
@inject IDocumentGalleryService GalleryService
@inject ICASStorage CASStorage
@inject IDocumentSigner Signer

<div class="document-preview">
    @if (document != null)
    {
        <div class="preview-header">
            <h3>Document Details</h3>
            <div class="signature-status">
                @if (isSignatureValid)
                {
                    <span class="valid">✓ Signature Valid</span>
                }
                else
                {
                    <span class="invalid">✗ Signature Invalid</span>
                }
            </div>
        </div>
        
        <div class="preview-content">
            <div class="image-section">
                <ImageViewer ImageData="@imageData" />
            </div>
            
            @if (document.OcrHash != null && ocrResult != null)
            {
                <div class="ocr-section">
                    <OCRTextViewer OcrData="@ocrResult" />
                </div>
            }
            else if (document.State == ProcessingState.OcrProcessing)
            {
                <div class="processing-indicator">
                    <PollingProgress DocumentId="@DocumentId" />
                </div>
            }
            else if (document.State == ProcessingState.OcrFailed)
            {
                <div class="error-section">
                    <p>OCR processing failed</p>
                    <RetryButton DocumentId="@DocumentId" OnRetry="RetryOcr" />
                </div>
            }
        </div>
        
        <div class="preview-metadata">
            <DocumentMetadataDisplay Metadata="@document.Metadata" />
        </div>
    </div>
</div>

@code {
    [Parameter] public Guid DocumentId { get; set; }
    
    private SignedDocument? document;
    private byte[]? imageData;
    private OcrResult? ocrResult;
    private bool isSignatureValid;
    
    protected override async Task OnParametersSetAsync()
    {
        await LoadDocument();
    }
    
    private async Task LoadDocument()
    {
        var details = await GalleryService.GetDocumentDetailsAsync(DocumentId);
        document = details.Document;
        
        // Verify signature
        isSignatureValid = await Signer.VerifyDocumentAsync(document);
        
        // Load image from CAS
        imageData = await CASStorage.RetrieveAsync(document.ImageHash);
        
        // Load OCR if available
        if (!string.IsNullOrEmpty(document.OcrHash))
        {
            var ocrData = await CASStorage.RetrieveAsync(document.OcrHash);
            ocrResult = JsonSerializer.Deserialize<OcrResult>(ocrData);
        }
        
        StateHasChanged();
    }
    
    private async Task RetryOcr()
    {
        // Re-submit for OCR processing
        await OcrService.RetryProcessingAsync(DocumentId);
        document.State = ProcessingState.OcrPending;
        StateHasChanged();
    }
}
```

## Content-Addressable Storage (CAS) Integration

### CAS Architecture

```mermaid
graph TB
    subgraph "CAS Layer"
        HASH[Hash Calculator]
        STORE[Block Store]
        INDEX[Content Index]
        DEDUP[Deduplication]
    end
    
    subgraph "Storage Operations"
        PUT[PUT: Content → Hash]
        GET[GET: Hash → Content]
        EXISTS[EXISTS: Hash → Boolean]
        DELETE[DELETE: Hash → Void]
    end
    
    subgraph "Content Types"
        IMG[Images<br/>JPEG/PNG/WebP]
        OCR[OCR Results<br/>JSON]
        DOC[Documents<br/>Signed JSON]
    end
    
    IMG --> HASH
    OCR --> HASH
    DOC --> HASH
    
    HASH --> DEDUP
    DEDUP --> STORE
    STORE --> INDEX
    
    PUT --> STORE
    GET --> STORE
    EXISTS --> INDEX
    DELETE --> STORE
```

### CAS Service Interface

```csharp
public interface ICASStorage
{
    Task<string> StoreAsync(byte[] content);
    Task<byte[]> RetrieveAsync(string hash);
    Task<bool> ExistsAsync(string hash);
    Task DeleteAsync(string hash);
    Task<CASMetadata> GetMetadataAsync(string hash);
}

public class CASStorage : ICASStorage
{
    private readonly IBlockStore _blockStore;
    private readonly IHashCalculator _hashCalculator;
    private readonly IContentIndex _index;
    
    public async Task<string> StoreAsync(byte[] content)
    {
        // Calculate SHA-256 hash
        var hash = _hashCalculator.ComputeSHA256(content);
        
        // Check for deduplication
        if (await _index.ExistsAsync(hash))
        {
            _logger.LogDebug("Content already exists: {Hash}", hash);
            return hash;
        }
        
        // Store content blocks
        await _blockStore.StoreBlocksAsync(hash, content);
        
        // Update index
        await _index.AddAsync(hash, new ContentMetadata
        {
            Size = content.Length,
            StoredAt = DateTime.UtcNow,
            ContentType = DetectContentType(content)
        });
        
        return hash;
    }
    
    public async Task<byte[]> RetrieveAsync(string hash)
    {
        if (!await _index.ExistsAsync(hash))
        {
            throw new ContentNotFoundException($"Content not found: {hash}");
        }
        
        return await _blockStore.RetrieveBlocksAsync(hash);
    }
}
```

## Pluggable Document Processor Architecture

### Overview

The pluggable document processor architecture enables seamless addition of new document types without modifying the core OCR scanning system. Each document type (receipt, check, W4, W2, 1099, etc.) is handled by a dedicated processor plugin that implements a common interface while providing document-specific logic for parsing, validation, and data extraction.

### Architecture Principles

1. **Open/Closed Principle**: System is open for extension (new document types) but closed for modification (core pipeline remains unchanged)
2. **Single Responsibility**: Each document processor handles exactly one document type
3. **Dependency Inversion**: Core system depends on abstractions (interfaces), not concrete implementations
4. **Plugin Discovery**: Automatic registration of new document processors via reflection or configuration
5. **Pipeline Consistency**: All documents flow through the same processing stages

### High-Level Plugin Architecture

```mermaid
graph TB
    subgraph "API Layer"
        API[API Gateway]
        R1[/receipts endpoint]
        R2[/checks endpoint]
        R3[/w4 endpoint]
        R4[/w2 endpoint]
        R5[/1099 endpoint]
    end
    
    subgraph "Routing Layer"
        ROUTER[Document Router]
        REGISTRY[Processor Registry]
    end
    
    subgraph "Core Pipeline"
        PIPELINE[Processing Pipeline]
        PRE[Preprocessing Stage]
        OCR[OCR Stage]
        POST[Postprocessing Stage]
        VAL[Validation Stage]
        TRANS[Transform Stage]
    end
    
    subgraph "Plugin Layer"
        P1[Receipt Processor]
        P2[Check Processor]
        P3[W4 Processor]
        P4[W2 Processor]
        P5[1099 Processor]
        PNEW[... Future Processors]
    end
    
    subgraph "Storage"
        CAS[Content-Addressable Storage]
        META[Metadata Store]
    end
    
    R1 --> API
    R2 --> API
    R3 --> API
    R4 --> API
    R5 --> API
    
    API --> ROUTER
    ROUTER --> REGISTRY
    REGISTRY --> PIPELINE
    
    PIPELINE --> PRE
    PRE --> OCR
    OCR --> POST
    POST --> VAL
    VAL --> TRANS
    
    P1 -.->|implements| PIPELINE
    P2 -.->|implements| PIPELINE
    P3 -.->|implements| PIPELINE
    P4 -.->|implements| PIPELINE
    P5 -.->|implements| PIPELINE
    PNEW -.->|implements| PIPELINE
    
    TRANS --> CAS
    TRANS --> META
    
    style PIPELINE fill:#f9f,stroke:#333,stroke-width:2px
    style REGISTRY fill:#9ff,stroke:#333,stroke-width:2px
    style PNEW stroke-dasharray: 5 5
```

### Core Interfaces

#### IDocumentProcessor Interface

```csharp
public interface IDocumentProcessor
{
    // Metadata
    DocumentType DocumentType { get; }
    string ProcessorVersion { get; }
    string[] SupportedFormats { get; }
    
    // Validation
    Task<ValidationResult> ValidateImageAsync(byte[] imageData);
    Task<ValidationResult> ValidateOcrResultAsync(OcrRawResult ocrResult);
    
    // Processing
    Task<PreprocessingResult> PreprocessAsync(byte[] imageData, ProcessingContext context);
    Task<ExtractionResult> ExtractDataAsync(OcrRawResult ocrResult, ProcessingContext context);
    Task<TransformationResult> TransformAsync(ExtractionResult extracted, ProcessingContext context);
    
    // Configuration
    ProcessorConfiguration GetConfiguration();
    Task<bool> CanProcessAsync(DocumentMetadata metadata);
}

public interface IDocumentProcessorPlugin : IDocumentProcessor
{
    // Plugin lifecycle
    Task InitializeAsync(IServiceProvider services);
    Task<HealthCheckResult> HealthCheckAsync();
    void Dispose();
    
    // Plugin metadata
    PluginMetadata GetMetadata();
}
```

#### Processing Context

```csharp
public class ProcessingContext
{
    public Guid DocumentId { get; set; }
    public DocumentType DocumentType { get; set; }
    public ProcessingOptions Options { get; set; }
    public Dictionary<string, object> CustomData { get; set; }
    public ILogger Logger { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class ProcessingOptions
{
    public bool EnhanceImage { get; set; }
    public bool AutoRotate { get; set; }
    public string Language { get; set; } = "en";
    public OcrEngine PreferredEngine { get; set; }
    public ValidationLevel ValidationLevel { get; set; }
    public Dictionary<string, string> ProcessorSpecificOptions { get; set; }
}
```

### Document Processor Registry

```csharp
public interface IDocumentProcessorRegistry
{
    Task RegisterProcessorAsync(IDocumentProcessorPlugin processor);
    Task UnregisterProcessorAsync(DocumentType type);
    IDocumentProcessor GetProcessor(DocumentType type);
    IEnumerable<IDocumentProcessor> GetAllProcessors();
    bool IsTypeSupported(DocumentType type);
    Task<IDocumentProcessor> DiscoverAndLoadAsync(string assemblyPath);
}

public class DocumentProcessorRegistry : IDocumentProcessorRegistry
{
    private readonly ConcurrentDictionary<DocumentType, IDocumentProcessorPlugin> _processors;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentProcessorRegistry> _logger;
    
    public async Task RegisterProcessorAsync(IDocumentProcessorPlugin processor)
    {
        var metadata = processor.GetMetadata();
        _logger.LogInformation("Registering processor: {Name} v{Version} for {Type}", 
            metadata.Name, metadata.Version, processor.DocumentType);
        
        // Initialize the processor
        await processor.InitializeAsync(_serviceProvider);
        
        // Verify health
        var health = await processor.HealthCheckAsync();
        if (health.Status != HealthStatus.Healthy)
        {
            throw new ProcessorRegistrationException(
                $"Processor {metadata.Name} failed health check: {health.Message}");
        }
        
        // Register
        _processors[processor.DocumentType] = processor;
        
        // Emit registration event
        await _eventBus.PublishAsync(new ProcessorRegisteredEvent
        {
            DocumentType = processor.DocumentType,
            ProcessorName = metadata.Name,
            Version = metadata.Version
        });
    }
    
    public IDocumentProcessor GetProcessor(DocumentType type)
    {
        if (_processors.TryGetValue(type, out var processor))
        {
            return processor;
        }
        
        throw new ProcessorNotFoundException($"No processor registered for type: {type}");
    }
}
```

### Processing Pipeline

```csharp
public interface IDocumentProcessingPipeline
{
    Task<ProcessingResult> ProcessAsync(
        byte[] imageData, 
        DocumentType documentType,
        ProcessingContext context);
}

public class DocumentProcessingPipeline : IDocumentProcessingPipeline
{
    private readonly IDocumentProcessorRegistry _registry;
    private readonly IOcrService _ocrService;
    private readonly ICASStorage _casStorage;
    private readonly IDocumentSigner _signer;
    private readonly IMetricsCollector _metrics;
    
    public async Task<ProcessingResult> ProcessAsync(
        byte[] imageData, 
        DocumentType documentType,
        ProcessingContext context)
    {
        using var activity = Activity.StartActivity("DocumentProcessing");
        activity?.SetTag("document.type", documentType.ToString());
        
        // Get appropriate processor
        var processor = _registry.GetProcessor(documentType);
        
        // Stage 1: Validation
        var validationResult = await processor.ValidateImageAsync(imageData);
        if (!validationResult.IsValid)
        {
            return ProcessingResult.ValidationFailed(validationResult.Errors);
        }
        
        // Stage 2: Preprocessing
        var preprocessResult = await processor.PreprocessAsync(imageData, context);
        
        // Stage 3: OCR Processing
        var ocrResult = await _ocrService.ProcessAsync(
            preprocessResult.ProcessedImage,
            new OcrOptions
            {
                Language = context.Options.Language,
                Engine = context.Options.PreferredEngine
            });
        
        // Stage 4: OCR Validation
        var ocrValidation = await processor.ValidateOcrResultAsync(ocrResult);
        if (!ocrValidation.IsValid)
        {
            return ProcessingResult.OcrValidationFailed(ocrValidation.Errors);
        }
        
        // Stage 5: Data Extraction
        var extractionResult = await processor.ExtractDataAsync(ocrResult, context);
        
        // Stage 6: Transformation
        var transformResult = await processor.TransformAsync(extractionResult, context);
        
        // Stage 7: Storage
        var storageResult = await StoreResultsAsync(
            imageData, 
            preprocessResult,
            ocrResult, 
            transformResult,
            processor.DocumentType);
        
        // Record metrics
        await _metrics.RecordProcessingAsync(new ProcessingMetrics
        {
            DocumentType = documentType,
            ProcessorVersion = processor.ProcessorVersion,
            ProcessingTime = activity?.Duration ?? TimeSpan.Zero,
            Success = true
        });
        
        return ProcessingResult.Success(storageResult);
    }
}
```

### Example Document Processor Implementations

#### Receipt Processor Plugin

```csharp
public class ReceiptProcessor : IDocumentProcessorPlugin
{
    public DocumentType DocumentType => DocumentType.Receipt;
    public string ProcessorVersion => "1.0.0";
    public string[] SupportedFormats => new[] { "image/jpeg", "image/png", "application/pdf" };
    
    private IReceiptParser _parser;
    private IReceiptValidator _validator;
    private ITaxCalculator _taxCalculator;
    
    public async Task<ValidationResult> ValidateImageAsync(byte[] imageData)
    {
        var errors = new List<string>();
        
        // Check image size
        if (imageData.Length > 10_000_000) // 10MB
            errors.Add("Image size exceeds 10MB limit");
        
        // Check image format
        var format = ImageFormatDetector.Detect(imageData);
        if (!SupportedFormats.Contains(format))
            errors.Add($"Unsupported format: {format}");
        
        // Check image quality
        var quality = await ImageQualityAnalyzer.AnalyzeAsync(imageData);
        if (quality.Score < 0.5)
            errors.Add("Image quality too low for reliable OCR");
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    public async Task<ExtractionResult> ExtractDataAsync(
        OcrRawResult ocrResult, 
        ProcessingContext context)
    {
        var receipt = new ReceiptData();
        
        // Extract merchant information
        receipt.MerchantName = await _parser.ExtractMerchantAsync(ocrResult.Text);
        receipt.MerchantAddress = await _parser.ExtractAddressAsync(ocrResult.Text);
        receipt.MerchantPhone = await _parser.ExtractPhoneAsync(ocrResult.Text);
        
        // Extract line items
        receipt.LineItems = await _parser.ExtractLineItemsAsync(ocrResult.Lines);
        
        // Extract totals
        receipt.Subtotal = await _parser.ExtractAmountAsync(ocrResult.Text, "subtotal");
        receipt.Tax = await _parser.ExtractAmountAsync(ocrResult.Text, "tax");
        receipt.Total = await _parser.ExtractAmountAsync(ocrResult.Text, "total");
        
        // Extract date/time
        receipt.TransactionDate = await _parser.ExtractDateAsync(ocrResult.Text);
        
        // Validate extracted data
        var validation = await _validator.ValidateReceiptAsync(receipt);
        
        return new ExtractionResult
        {
            Data = receipt,
            Confidence = CalculateConfidence(receipt, ocrResult),
            ValidationResult = validation
        };
    }
    
    public PluginMetadata GetMetadata()
    {
        return new PluginMetadata
        {
            Name = "Receipt Processor",
            Description = "Processes retail receipts and extracts transaction data",
            Author = "NoLock Team",
            Version = ProcessorVersion,
            MinimumSystemVersion = "1.0.0",
            Dependencies = new[] { "TaxCalculator", "CurrencyConverter" }
        };
    }
}
```

#### W4 Tax Form Processor Plugin

```csharp
public class W4Processor : IDocumentProcessorPlugin
{
    public DocumentType DocumentType => DocumentType.W4;
    public string ProcessorVersion => "1.0.0";
    
    private IIrsFormValidator _irsValidator;
    private ITaxFormParser _formParser;
    
    public async Task<ValidationResult> ValidateOcrResultAsync(OcrRawResult ocrResult)
    {
        var errors = new List<string>();
        
        // Verify form number
        if (!ocrResult.Text.Contains("Form W-4", StringComparison.OrdinalIgnoreCase))
            errors.Add("Document does not appear to be a W-4 form");
        
        // Check for required fields
        var requiredFields = new[] 
        { 
            "First name", 
            "Last name", 
            "Social Security Number",
            "Filing Status"
        };
        
        foreach (var field in requiredFields)
        {
            if (!ContainsField(ocrResult, field))
                errors.Add($"Required field missing: {field}");
        }
        
        // Verify form year
        var year = ExtractFormYear(ocrResult.Text);
        if (year < DateTime.Now.Year - 1)
            errors.Add($"Form year {year} is outdated");
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    public async Task<ExtractionResult> ExtractDataAsync(
        OcrRawResult ocrResult, 
        ProcessingContext context)
    {
        var w4Data = new W4FormData();
        
        // Extract employee information
        w4Data.FirstName = await _formParser.ExtractFieldAsync(ocrResult, "First name");
        w4Data.LastName = await _formParser.ExtractFieldAsync(ocrResult, "Last name");
        w4Data.SSN = await _formParser.ExtractSSNAsync(ocrResult);
        w4Data.Address = await _formParser.ExtractAddressBlockAsync(ocrResult);
        
        // Extract filing status
        w4Data.FilingStatus = await _formParser.ExtractCheckboxAsync(ocrResult, 
            new[] { "Single", "Married filing jointly", "Head of household" });
        
        // Extract withholding information
        w4Data.MultipleJobs = await _formParser.ExtractCheckboxAsync(ocrResult, "Multiple jobs");
        w4Data.DependentsAmount = await _formParser.ExtractCurrencyAsync(ocrResult, "Step 3");
        w4Data.OtherIncome = await _formParser.ExtractCurrencyAsync(ocrResult, "Step 4(a)");
        w4Data.Deductions = await _formParser.ExtractCurrencyAsync(ocrResult, "Step 4(b)");
        w4Data.ExtraWithholding = await _formParser.ExtractCurrencyAsync(ocrResult, "Step 4(c)");
        
        // Extract signature
        w4Data.HasSignature = await _formParser.DetectSignatureAsync(ocrResult);
        w4Data.SignatureDate = await _formParser.ExtractDateAsync(ocrResult, "Date");
        
        // Validate with IRS rules
        var validation = await _irsValidator.ValidateW4Async(w4Data);
        
        return new ExtractionResult
        {
            Data = w4Data,
            Confidence = CalculateFormConfidence(w4Data, ocrResult),
            ValidationResult = validation
        };
    }
}
```

### Plugin Configuration

```yaml
# document-processors.yaml
processors:
  - type: Receipt
    enabled: true
    assembly: NoLock.Processors.Receipt.dll
    className: NoLock.Processors.Receipt.ReceiptProcessor
    configuration:
      maxImageSize: 10485760  # 10MB
      minQualityScore: 0.5
      supportedCurrencies: ["USD", "EUR", "GBP"]
      
  - type: Check
    enabled: true
    assembly: NoLock.Processors.Banking.dll
    className: NoLock.Processors.Banking.CheckProcessor
    configuration:
      micr:
        enabled: true
        validateChecksum: true
      routing:
        validateWithABA: true
        
  - type: W4
    enabled: true
    assembly: NoLock.Processors.TaxForms.dll
    className: NoLock.Processors.TaxForms.W4Processor
    configuration:
      formYear: 2024
      validateSSN: true
      requireSignature: true
      
  - type: W2
    enabled: false  # Coming soon
    assembly: NoLock.Processors.TaxForms.dll
    className: NoLock.Processors.TaxForms.W2Processor
    
  - type: Form1099
    enabled: false  # Coming soon
    assembly: NoLock.Processors.TaxForms.dll
    className: NoLock.Processors.TaxForms.Form1099Processor
```

### Plugin Loading and Discovery

```csharp
public class PluginLoader
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginLoader> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    public async Task<IEnumerable<IDocumentProcessorPlugin>> LoadPluginsAsync()
    {
        var plugins = new List<IDocumentProcessorPlugin>();
        var config = _configuration.GetSection("processors").Get<ProcessorConfig[]>();
        
        foreach (var processorConfig in config.Where(c => c.Enabled))
        {
            try
            {
                var plugin = await LoadPluginAsync(processorConfig);
                plugins.Add(plugin);
                _logger.LogInformation("Loaded plugin: {Type} from {Assembly}", 
                    processorConfig.Type, processorConfig.Assembly);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin: {Type}", processorConfig.Type);
            }
        }
        
        return plugins;
    }
    
    private async Task<IDocumentProcessorPlugin> LoadPluginAsync(ProcessorConfig config)
    {
        // Load assembly
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "plugins", config.Assembly);
        var assembly = Assembly.LoadFrom(assemblyPath);
        
        // Find and instantiate processor type
        var processorType = assembly.GetType(config.ClassName);
        if (processorType == null)
        {
            throw new TypeLoadException($"Type {config.ClassName} not found in {config.Assembly}");
        }
        
        // Create instance with dependency injection
        var processor = (IDocumentProcessorPlugin)ActivatorUtilities.CreateInstance(
            _serviceProvider, processorType);
        
        // Apply configuration
        if (config.Configuration != null)
        {
            await processor.ConfigureAsync(config.Configuration);
        }
        
        return processor;
    }
}
```

### Dynamic Endpoint Registration

```csharp
public class DynamicEndpointRegistration
{
    public static void RegisterDocumentEndpoints(
        this IEndpointRouteBuilder endpoints, 
        IDocumentProcessorRegistry registry)
    {
        foreach (var processor in registry.GetAllProcessors())
        {
            var documentType = processor.DocumentType;
            var routePath = $"/api/ocr/{documentType.ToString().ToLower()}";
            
            endpoints.MapPost(routePath, async (
                HttpRequest request,
                IDocumentProcessingPipeline pipeline,
                ILogger<Program> logger) =>
            {
                try
                {
                    // Read image from request
                    using var ms = new MemoryStream();
                    await request.Body.CopyToAsync(ms);
                    var imageData = ms.ToArray();
                    
                    // Create processing context
                    var context = new ProcessingContext
                    {
                        DocumentId = Guid.NewGuid(),
                        DocumentType = documentType,
                        Options = GetOptionsFromHeaders(request.Headers),
                        Logger = logger,
                        CancellationToken = request.HttpContext.RequestAborted
                    };
                    
                    // Process document
                    var result = await pipeline.ProcessAsync(imageData, documentType, context);
                    
                    return result.Success 
                        ? Results.Ok(result.Data)
                        : Results.BadRequest(result.Errors);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing {DocumentType}", documentType);
                    return Results.Problem("An error occurred processing the document");
                }
            })
            .WithName($"Process{documentType}")
            .WithOpenApi(operation =>
            {
                operation.Summary = $"Process {documentType} document";
                operation.Description = $"Submits a {documentType} for OCR processing";
                return operation;
            })
            .RequireAuthorization()
            .DisableAntiforgery();
        }
    }
}
```

### Error Handling and Validation Rules

```csharp
public abstract class BaseDocumentProcessor : IDocumentProcessorPlugin
{
    protected readonly ILogger _logger;
    protected readonly IValidationRuleEngine _validationEngine;
    
    public async Task<ValidationResult> ValidateWithRulesAsync<T>(T data)
    {
        var rules = GetValidationRules();
        var results = new List<ValidationError>();
        
        foreach (var rule in rules)
        {
            var result = await rule.ValidateAsync(data);
            if (!result.IsValid)
            {
                results.AddRange(result.Errors);
            }
        }
        
        return new ValidationResult
        {
            IsValid = results.Count == 0,
            Errors = results.Select(e => e.Message).ToList()
        };
    }
    
    protected abstract IEnumerable<IValidationRule> GetValidationRules();
}

public interface IValidationRule
{
    string RuleName { get; }
    ValidationLevel Level { get; }
    Task<ValidationResult> ValidateAsync(object data);
}

public class RequiredFieldRule : IValidationRule
{
    public string RuleName => "RequiredField";
    public ValidationLevel Level => ValidationLevel.Error;
    
    private readonly string _fieldName;
    private readonly Func<object, string> _fieldExtractor;
    
    public async Task<ValidationResult> ValidateAsync(object data)
    {
        var value = _fieldExtractor(data);
        
        return string.IsNullOrWhiteSpace(value)
            ? ValidationResult.Failed($"Required field '{_fieldName}' is missing")
            : ValidationResult.Success();
    }
}
```

### Performance Optimization with Processor Caching

```csharp
public class CachedProcessorRegistry : IDocumentProcessorRegistry
{
    private readonly IDocumentProcessorRegistry _innerRegistry;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    
    public IDocumentProcessor GetProcessor(DocumentType type)
    {
        return _cache.GetOrCreate($"processor_{type}", entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            entry.RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                if (value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            });
            
            return _innerRegistry.GetProcessor(type);
        });
    }
    
    public async Task WarmupProcessorsAsync(IEnumerable<DocumentType> types)
    {
        var tasks = types.Select(async type =>
        {
            await _loadLock.WaitAsync();
            try
            {
                var processor = GetProcessor(type);
                if (processor is IDocumentProcessorPlugin plugin)
                {
                    await plugin.HealthCheckAsync();
                }
            }
            finally
            {
                _loadLock.Release();
            }
        });
        
        await Task.WhenAll(tasks);
    }
}
```

### Monitoring and Metrics

```csharp
public class ProcessorMetricsCollector
{
    private readonly IMetrics _metrics;
    
    public void RecordProcessing(DocumentType type, TimeSpan duration, bool success)
    {
        _metrics.Measure.Counter.Increment(
            new CounterOptions 
            { 
                Name = "document_processing_total",
                Tags = new MetricTags("type", type.ToString(), "success", success.ToString())
            });
        
        _metrics.Measure.Histogram.Update(
            new HistogramOptions
            {
                Name = "document_processing_duration_seconds",
                Tags = new MetricTags("type", type.ToString())
            },
            (long)duration.TotalMilliseconds);
    }
    
    public void RecordValidationFailure(DocumentType type, string reason)
    {
        _metrics.Measure.Counter.Increment(
            new CounterOptions
            {
                Name = "document_validation_failures_total",
                Tags = new MetricTags("type", type.ToString(), "reason", reason)
            });
    }
}
```

## API Integration Design

### Proxy Generation Strategy

#### Using NSwag for Client Generation

```bash
# Install NSwag CLI tool
dotnet tool install -g NSwag.ConsoleCore

# Generate C# client from OpenAPI specification
nswag openapi2csclient /input:https://nolock-ocr-services-qbhx5.ondigitalocean.app/swagger/v1/swagger.json \
  /classname:OCRServiceClient \
  /namespace:NoLock.Social.DocumentScanner.Services.Proxies \
  /output:NoLock.Social.DocumentScanner/Services/Proxies/OCRServiceClient.cs \
  /generateClientInterfaces:true \
  /generateDtoTypes:true \
  /generateResponseClasses:true \
  /generateDataAnnotations:true \
  /generateDefaultValues:true \
  /generateOptionalParameters:true
```

#### Alternative: OpenAPI Generator

```bash
# Install OpenAPI Generator
npm install -g @openapitools/openapi-generator-cli

# Generate C# client
openapi-generator-cli generate \
  -i https://nolock-ocr-services-qbhx5.ondigitalocean.app/swagger/v1/swagger.json \
  -g csharp-netcore \
  -o ./generated-client \
  --additional-properties=packageName=NoLock.Social.DocumentScanner.Client \
  --additional-properties=netCoreProjectFile=true \
  --additional-properties=targetFramework=net9.0
```

### Service Integration Pattern

```csharp
public interface IOCRServiceAdapter
{
    Task<ReceiptData> ProcessReceiptAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<CheckData> ProcessCheckAsync(byte[] imageData, CancellationToken cancellationToken = default);
    Task<HealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);
}

public class OCRServiceAdapter : IOCRServiceAdapter
{
    private readonly IOCRServiceClient _client;
    private readonly ILogger<OCRServiceAdapter> _logger;
    private readonly IRetryPolicy _retryPolicy;
    
    public async Task<ReceiptData> ProcessReceiptAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using var stream = new MemoryStream(imageData);
            var response = await _client.ProcessReceiptAsync(stream, cancellationToken);
            return MapToReceiptData(response);
        });
    }
}
```

## UI/UX Design

### Mobile Layout (ASCII Art)

```
+------------------------+
|   Document Scanner     |
|   [Camera] [Gallery]   |
+------------------------+
|                        |
|   +----------------+   |
|   |                |   |
|   |    CAMERA      |   |
|   |     VIEW       |   |
|   |                |   |
|   |  [+] Target    |   |
|   |                |   |
|   +----------------+   |
|                        |
|  [Flash] [📷] [Switch] |
|                        |
|  Processing: ○○○○○     |
|  2 docs pending OCR    |
+------------------------+
```

### Gallery View - Mobile (ASCII Art)

```
+------------------------+
|   Document Gallery     |
|   [Camera] [Gallery]   |
+------------------------+
| Filter: [All Types  v] |
| Search: [___________]  |
+------------------------+
| +------+ +------+      |
| |      | |      |      |
| | IMG1 | | IMG2 |      |
| |  ✓   | |  ⟳   |      |
| +------+ +------+      |
| Receipt  Check         |
|                        |
| +------+ +------+      |
| |      | |      |      |
| | IMG3 | | IMG4 |      |
| |  ✓   | |  ✗   |      |
| +------+ +------+      |
| Receipt  Receipt       |
+------------------------+
| ✓ Complete ⟳ Processing|
| ✗ Failed               |
+------------------------+
```

### Desktop Layout with Gallery (ASCII Art)

```
+----------------------------------------------------------+
|  Document Scanner                          [Settings] [?] |
+----------------------------------------------------------+
|                                                          |
| +----------------------+  +----------------------------+ |
| |                      |  | Document Gallery           | |
| |     CAMERA VIEW      |  | +------+ +------+ +------+ | |
| |        -OR-          |  | | Doc1 | | Doc2 | | Doc3 | | |
| |   DOCUMENT PREVIEW   |  | |  ✓   | |  ⟳   | |  ✓   | | |
| |                      |  | +------+ +------+ +------+ | |
| |    [+] Viewfinder    |  |                            | |
| |                      |  | +------+ +------+ +------+ | |
| |                      |  | | Doc4 | | Doc5 | | Doc6 | | |
| +----------------------+  | |  ✗   | |  ✓   | |  ⟳   | | |
| |                      |  | +------+ +------+ +------+ | |
| | [Capture] [Gallery]  |  |                            | |
| |                      |  | Processing Status:          | |
| | OCR Status: [=====]  |  | • 2 documents processing   | |
| | Time: 45s / 120s     |  | • 1 document failed        | |
| +----------------------+  | • 8 documents complete      | |
|                           +----------------------------+ |
+----------------------------------------------------------+
```

### Responsive Design Strategy

```mermaid
graph LR
    A[Screen Detection] --> B{Screen Size?}
    B -->|Mobile < 768px| C[Mobile Layout]
    B -->|Tablet 768-1024px| D[Tablet Layout]
    B -->|Desktop > 1024px| E[Desktop Layout]
    
    C --> F[Full Screen Camera]
    D --> G[Side Panel Layout]
    E --> H[Multi-Column Layout]
    
    F --> I[Bottom Controls]
    G --> J[Right Panel Controls]
    H --> K[Sidebar + Main Area]
```

## Project Structure

### NoLock.Social.DocumentScanner Project Organization

```
NoLock.Social.DocumentScanner/
├── Components/
│   ├── Camera/
│   │   ├── CameraViewComponent.razor
│   │   ├── CameraViewComponent.razor.cs
│   │   ├── CameraViewComponent.razor.css
│   │   └── ViewfinderOverlay.razor
│   ├── Gallery/
│   │   ├── DocumentGalleryComponent.razor
│   │   ├── DocumentGrid.razor
│   │   ├── ThumbnailView.razor
│   │   └── FilterControls.razor
│   ├── Preview/
│   │   ├── DocumentPreviewComponent.razor
│   │   ├── ImageViewer.razor
│   │   ├── OCRTextViewer.razor
│   │   └── DocumentMetadata.razor
│   ├── Status/
│   │   ├── ProcessingStatusComponent.razor
│   │   ├── PollingProgress.razor
│   │   └── RetryControls.razor
│   ├── Controls/
│   │   ├── ScannerControlsComponent.razor
│   │   ├── ModeSelector.razor
│   │   └── ActionButtons.razor
│   └── DocumentScannerComponent.razor
├── Services/
│   ├── Interfaces/
│   │   ├── ICameraService.cs
│   │   ├── IImageProcessingService.cs
│   │   ├── IOCRServiceAdapter.cs
│   │   ├── IScannerService.cs
│   │   ├── IPollingService.cs
│   │   ├── IDocumentSigner.cs
│   │   ├── ICASStorage.cs
│   │   └── IKeyManager.cs
│   ├── Implementation/
│   │   ├── CameraService.cs
│   │   ├── ImageProcessingService.cs
│   │   ├── OCRServiceAdapter.cs
│   │   ├── ScannerService.cs
│   │   ├── AdaptivePollingService.cs
│   │   ├── Ed25519DocumentSigner.cs
│   │   ├── CASStorage.cs
│   │   └── SecureKeyManager.cs
│   ├── Background/
│   │   └── OcrBackgroundProcessor.cs
│   ├── Proxies/
│   │   └── OCRServiceClient.cs (generated)
│   └── JavaScript/
│       └── CameraInterop.cs
├── Models/
│   ├── ScannerState.cs
│   ├── CapturedImage.cs
│   ├── OCRResult.cs
│   ├── SignedDocument.cs
│   ├── DocumentMetadata.cs
│   ├── ProcessingState.cs
│   ├── OcrProcessingJob.cs
│   ├── CASMetadata.cs
│   ├── Ed25519KeyPair.cs
│   ├── ReceiptData.cs
│   ├── CheckData.cs
│   └── DocumentType.cs
├── State/
│   ├── ScannerStateManager.cs
│   ├── IState.cs
│   └── StateContainer.cs
├── Configuration/
│   ├── ScannerConfiguration.cs
│   └── OCRServiceConfiguration.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
├── wwwroot/
│   ├── css/
│   │   └── scanner.css
│   ├── js/
│   │   ├── camera-interop.js
│   │   └── image-processing.js
│   └── assets/
│       └── viewfinder-overlay.svg
└── NoLock.Social.DocumentScanner.csproj
```

### Dependencies Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
    <PackageReference Include="NSwag.MSBuild" Version="14.0.0" />
    <PackageReference Include="System.Reactive" Version="6.0.0" />
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.0" />
    <PackageReference Include="Blazored.LocalStorage" Version="4.4.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\NoLock.Social.Core\NoLock.Social.Core.csproj" />
  </ItemGroup>
</Project>
```

## Technology Stack

### Core Technologies
- **Blazor WebAssembly**: Component framework
- **NET 8.0**: Target framework
- **.NET Standard 2.1**: Library compatibility

### UI/UX Libraries
- **Bootstrap 5**: Responsive layout
- **Blazored.Modal**: Modal dialogs
- **Blazored.Toast**: Notifications

### Image Processing
- **SixLabors.ImageSharp**: Client-side image manipulation
- **Browser Canvas API**: Image cropping and enhancement

### API Integration
- **NSwag**: OpenAPI client generation
- **System.Net.Http**: HTTP communication
- **Polly**: Retry and resilience policies

### State Management
- **System.Reactive**: Reactive state management
- **Blazored.LocalStorage**: Offline storage

### JavaScript Interop
- **MediaDevices API**: Camera access
- **Canvas API**: Image manipulation
- **File API**: Image upload/download

## Implementation Details

### Camera Integration

```csharp
public class CameraService : ICameraService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _cameraModule;
    
    public async Task<MediaStream> StartCameraAsync(CameraOptions options)
    {
        _cameraModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/camera-interop.js");
            
        return await _cameraModule.InvokeAsync<MediaStream>(
            "startCamera", options);
    }
    
    public async Task<byte[]> CaptureFrameAsync()
    {
        if (_cameraModule == null)
            throw new InvalidOperationException("Camera not initialized");
            
        var base64 = await _cameraModule.InvokeAsync<string>("captureFrame");
        return Convert.FromBase64String(base64.Split(',')[1]);
    }
}
```

### JavaScript Camera Module

```javascript
// camera-interop.js
export async function startCamera(options) {
    const constraints = {
        video: {
            facingMode: options.facingMode || 'environment',
            width: { ideal: options.width || 1920 },
            height: { ideal: options.height || 1080 }
        }
    };
    
    try {
        const stream = await navigator.mediaDevices.getUserMedia(constraints);
        return stream;
    } catch (error) {
        console.error('Camera access error:', error);
        throw error;
    }
}

export function captureFrame(videoElement) {
    const canvas = document.createElement('canvas');
    canvas.width = videoElement.videoWidth;
    canvas.height = videoElement.videoHeight;
    
    const context = canvas.getContext('2d');
    context.drawImage(videoElement, 0, 0);
    
    return canvas.toDataURL('image/jpeg', 0.9);
}
```

### Image Preprocessing

```csharp
public class ImageProcessingService : IImageProcessingService
{
    public async Task<byte[]> PreprocessImageAsync(byte[] imageData, ProcessingOptions options)
    {
        using var image = Image.Load(imageData);
        
        // Auto-orient based on EXIF data
        image.Mutate(x => x.AutoOrient());
        
        // Apply preprocessing
        if (options.EnhanceContrast)
        {
            image.Mutate(x => x.Contrast(1.2f));
        }
        
        if (options.ConvertToGrayscale)
        {
            image.Mutate(x => x.Grayscale());
        }
        
        if (options.ResizeToMaxDimension.HasValue)
        {
            var maxDim = options.ResizeToMaxDimension.Value;
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxDim, maxDim)
            }));
        }
        
        using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output);
        return output.ToArray();
    }
}
```

### Error Handling and Retry Logic

```csharp
public class RetryPolicyConfiguration
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values["logger"] as ILogger;
                    logger?.LogWarning($"Retry {retryCount} after {timespan}s");
                });
    }
    
    public static IAsyncPolicy GetCircuitBreakerPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    // Log circuit breaker opening
                },
                onReset: () =>
                {
                    // Log circuit breaker closing
                });
    }
}
```

### Offline Support

```csharp
public class OfflineScanManager
{
    private readonly ILocalStorageService _localStorage;
    private readonly IOCRServiceAdapter _ocrService;
    
    public async Task QueueForProcessingAsync(CapturedImage image)
    {
        var queue = await GetQueueAsync();
        queue.Add(new QueuedScan
        {
            Id = Guid.NewGuid(),
            Image = image,
            QueuedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Pending
        });
        
        await _localStorage.SetItemAsync("scan_queue", queue);
    }
    
    public async Task ProcessQueueAsync()
    {
        var queue = await GetQueueAsync();
        var pending = queue.Where(x => x.Status == ProcessingStatus.Pending);
        
        foreach (var item in pending)
        {
            try
            {
                var result = await _ocrService.ProcessReceiptAsync(item.Image.Data);
                item.Status = ProcessingStatus.Completed;
                item.Result = result;
            }
            catch (Exception ex)
            {
                item.Status = ProcessingStatus.Failed;
                item.Error = ex.Message;
            }
        }
        
        await _localStorage.SetItemAsync("scan_queue", queue);
    }
}
```

## Error Handling and Resilience Patterns

### Comprehensive Error Management

```csharp
public class OcrProcessingErrorHandler
{
    private readonly ILogger<OcrProcessingErrorHandler> _logger;
    private readonly IDocumentRepository _documentRepo;
    private readonly INotificationService _notifications;
    
    public async Task HandleProcessingError(
        Guid documentId, 
        Exception exception,
        int attemptNumber)
    {
        var errorType = ClassifyError(exception);
        
        switch (errorType)
        {
            case ErrorType.Timeout:
                await HandleTimeout(documentId, attemptNumber);
                break;
                
            case ErrorType.RateLimited:
                await HandleRateLimit(documentId, attemptNumber);
                break;
                
            case ErrorType.NetworkError:
                await HandleNetworkError(documentId, attemptNumber);
                break;
                
            case ErrorType.InvalidImage:
                await HandleInvalidImage(documentId);
                break;
                
            case ErrorType.ServiceUnavailable:
                await HandleServiceUnavailable(documentId, attemptNumber);
                break;
                
            default:
                await HandleUnknownError(documentId, exception);
                break;
        }
    }
    
    private async Task HandleTimeout(Guid documentId, int attemptNumber)
    {
        if (attemptNumber < 3)
        {
            // Retry with exponential backoff
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber) * 10);
            await ScheduleRetry(documentId, delay);
            
            _logger.LogWarning(
                "OCR timeout for document {DocumentId}, retry {Attempt} scheduled in {Delay}",
                documentId, attemptNumber + 1, delay);
        }
        else
        {
            // Mark as failed after max retries
            await UpdateDocumentState(documentId, ProcessingState.OcrTimeout);
            await _notifications.NotifyUserAsync(
                documentId, 
                "OCR processing timed out after multiple attempts");
        }
    }
    
    private async Task HandleRateLimit(Guid documentId, int attemptNumber)
    {
        // Implement exponential backoff for rate limiting
        var delay = TimeSpan.FromMinutes(Math.Pow(2, attemptNumber));
        await ScheduleRetry(documentId, delay);
        
        _logger.LogInformation(
            "Rate limited for document {DocumentId}, retry scheduled in {Delay}",
            documentId, delay);
    }
    
    private ErrorType ClassifyError(Exception exception)
    {
        return exception switch
        {
            OcrTimeoutException => ErrorType.Timeout,
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => ErrorType.RateLimited,
            HttpRequestException { StatusCode: HttpStatusCode.ServiceUnavailable } => ErrorType.ServiceUnavailable,
            HttpRequestException => ErrorType.NetworkError,
            InvalidImageException => ErrorType.InvalidImage,
            _ => ErrorType.Unknown
        };
    }
}

public class CircuitBreakerOcrProxy : IOCRServiceProxy
{
    private readonly IOCRServiceProxy _innerProxy;
    private readonly ICircuitBreaker _circuitBreaker;
    
    public async Task<OcrStatus> GetStatusAsync(Guid taskId)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            return await _innerProxy.GetStatusAsync(taskId);
        });
    }
    
    public async Task<Guid> SubmitForProcessingAsync(byte[] imageData)
    {
        // Check for duplicate submissions
        var imageHash = ComputeHash(imageData);
        var cached = await CheckCache(imageHash);
        
        if (cached != null)
        {
            _logger.LogDebug("Returning cached OCR result for hash {Hash}", imageHash);
            return cached.TaskId;
        }
        
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            var taskId = await _innerProxy.SubmitForProcessingAsync(imageData);
            await CacheSubmission(imageHash, taskId);
            return taskId;
        });
    }
}
```

### Retry Strategy Configuration

```csharp
public class RetryConfiguration
{
    public static IAsyncPolicy<T> GetRetryPolicy<T>()
    {
        return Policy<T>
            .Handle<HttpRequestException>()
            .OrResult(r => ShouldRetry(r))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values["logger"] as ILogger;
                    logger?.LogWarning(
                        "Retry {RetryCount} after {Timespan}s for operation {Operation}",
                        retryCount, timespan.TotalSeconds, context.OperationKey);
                });
    }
    
    public static IAsyncPolicy GetCircuitBreakerPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    // Log and notify about circuit breaker opening
                    LogCircuitBreakerOpen(exception, duration);
                },
                onReset: () =>
                {
                    // Log circuit breaker closing
                    LogCircuitBreakerReset();
                },
                onHalfOpen: () =>
                {
                    // Log circuit breaker testing
                    LogCircuitBreakerHalfOpen();
                });
    }
}
```

## Security and Privacy

### Security Measures

1. **Data Protection**
   - Images processed in memory only
   - No permanent storage without user consent
   - Secure transmission using HTTPS
   - Client-side image preprocessing to minimize data transfer

2. **Permission Management**
   ```csharp
   public async Task<PermissionStatus> RequestCameraPermissionAsync()
   {
       try
       {
           var result = await _jsRuntime.InvokeAsync<PermissionResult>(
               "navigator.permissions.query", 
               new { name = "camera" });
           
           return result.State switch
           {
               "granted" => PermissionStatus.Granted,
               "denied" => PermissionStatus.Denied,
               "prompt" => PermissionStatus.Prompt,
               _ => PermissionStatus.Unknown
           };
       }
       catch
       {
           return PermissionStatus.Unknown;
       }
   }
   ```

3. **Content Security Policy**
   ```html
   <meta http-equiv="Content-Security-Policy" 
         content="default-src 'self'; 
                  media-src 'self' blob:; 
                  img-src 'self' data: blob:;
                  connect-src 'self' https://nolock-ocr-services-qbhx5.ondigitalocean.app;">
   ```

### Privacy Considerations

1. **Data Minimization**
   - Process images locally when possible
   - Send only necessary data to OCR service
   - Clear sensitive data from memory after use

2. **User Consent**
   - Explicit permission requests for camera access
   - Clear data usage disclosure
   - Option to process locally without sending to server

3. **Compliance**
   - GDPR compliance for EU users
   - CCPA compliance for California users
   - Right to data deletion

## State Management for Pending OCR Requests

### Processing Queue State Manager

```csharp
public class OcrProcessingStateManager
{
    private readonly ConcurrentDictionary<Guid, ProcessingState> _activeJobs;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<OcrStatusHub> _hubContext;
    
    public event EventHandler<ProcessingStateChangedEventArgs>? StateChanged;
    
    public async Task TrackJobAsync(OcrProcessingJob job)
    {
        _activeJobs[job.DocumentId] = new ProcessingState
        {
            DocumentId = job.DocumentId,
            TaskId = job.TaskId,
            Status = OcrStatus.Pending,
            StartedAt = DateTime.UtcNow,
            LastPolledAt = null,
            PollCount = 0
        };
        
        // Notify UI components
        await NotifyStateChange(job.DocumentId, OcrStatus.Pending);
    }
    
    public async Task UpdatePollingStatusAsync(Guid documentId, OcrStatus status)
    {
        if (_activeJobs.TryGetValue(documentId, out var state))
        {
            state.Status = status;
            state.LastPolledAt = DateTime.UtcNow;
            state.PollCount++;
            
            // Update cache for UI components
            _cache.Set($"ocr_status_{documentId}", state, TimeSpan.FromMinutes(5));
            
            // Send real-time update via SignalR
            await _hubContext.Clients.All.SendAsync("OcrStatusUpdate", new
            {
                DocumentId = documentId,
                Status = status.ToString(),
                Progress = CalculateProgress(state),
                Message = GetStatusMessage(status)
            });
            
            // Raise event for local subscribers
            StateChanged?.Invoke(this, new ProcessingStateChangedEventArgs(documentId, status));
        }
    }
    
    public async Task CompleteJobAsync(Guid documentId, OcrResult result)
    {
        if (_activeJobs.TryRemove(documentId, out var state))
        {
            state.Status = OcrStatus.Completed;
            state.CompletedAt = DateTime.UtcNow;
            
            // Store completion in cache
            _cache.Set($"ocr_result_{documentId}", result, TimeSpan.FromHours(1));
            
            // Notify all subscribers
            await NotifyStateChange(documentId, OcrStatus.Completed);
        }
    }
    
    private int CalculateProgress(ProcessingState state)
    {
        // Calculate progress based on elapsed time vs expected duration
        var elapsed = (DateTime.UtcNow - state.StartedAt).TotalSeconds;
        var expectedDuration = 90.0; // 1.5 minutes average
        
        return Math.Min(95, (int)(elapsed / expectedDuration * 100));
    }
    
    public IEnumerable<ProcessingState> GetActiveJobs()
    {
        return _activeJobs.Values
            .Where(j => j.Status != OcrStatus.Completed && j.Status != OcrStatus.Failed)
            .OrderBy(j => j.StartedAt);
    }
}

// SignalR Hub for real-time updates
public class OcrStatusHub : Hub
{
    private readonly OcrProcessingStateManager _stateManager;
    
    public async Task SubscribeToDocument(Guid documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doc_{documentId}");
        
        // Send current status immediately
        var status = _stateManager.GetJobStatus(documentId);
        if (status != null)
        {
            await Clients.Caller.SendAsync("CurrentStatus", status);
        }
    }
    
    public async Task UnsubscribeFromDocument(Guid documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"doc_{documentId}");
    }
}
```

### UI State Synchronization

```csharp
@implements IAsyncDisposable
@inject OcrProcessingStateManager StateManager
@inject NavigationManager Navigation

<div class="processing-dashboard">
    <h4>OCR Processing Queue</h4>
    
    @if (activeJobs.Any())
    {
        <div class="active-jobs">
            @foreach (var job in activeJobs)
            {
                <div class="job-card">
                    <div class="job-header">
                        <span>Document @job.DocumentId.ToString().Substring(0, 8)</span>
                        <span class="status-badge @GetStatusClass(job.Status)">
                            @job.Status
                        </span>
                    </div>
                    
                    <div class="job-progress">
                        <div class="progress-bar">
                            <div class="progress-fill" style="width: @(job.Progress)%"></div>
                        </div>
                        <span class="progress-text">@job.Progress%</span>
                    </div>
                    
                    <div class="job-timing">
                        <span>Started: @job.StartedAt.ToLocalTime().ToString("HH:mm:ss")</span>
                        @if (job.LastPolledAt.HasValue)
                        {
                            <span>Last check: @((DateTime.UtcNow - job.LastPolledAt.Value).TotalSeconds)s ago</span>
                        }
                    </div>
                    
                    @if (job.Status == OcrStatus.Failed || job.Status == OcrStatus.Timeout)
                    {
                        <button class="retry-btn" @onclick="() => RetryJob(job.DocumentId)">
                            Retry Processing
                        </button>
                    }
                </div>
            }
        </div>
    }
    else
    {
        <p class="no-jobs">No documents currently processing</p>
    }
</div>

@code {
    private List<ProcessingJobViewModel> activeJobs = new();
    private HubConnection? hubConnection;
    private Timer? refreshTimer;
    
    protected override async Task OnInitializedAsync()
    {
        // Load initial state
        await RefreshActiveJobs();
        
        // Set up SignalR connection
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/ocr-status"))
            .WithAutomaticReconnect()
            .Build();
        
        hubConnection.On<OcrStatusUpdate>("OcrStatusUpdate", async (update) =>
        {
            await InvokeAsync(() =>
            {
                UpdateJobStatus(update);
                StateHasChanged();
            });
        });
        
        await hubConnection.StartAsync();
        
        // Set up periodic refresh
        refreshTimer = new Timer(async _ => await RefreshActiveJobs(), 
            null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        // Subscribe to state changes
        StateManager.StateChanged += OnStateChanged;
    }
    
    private async Task RefreshActiveJobs()
    {
        activeJobs = StateManager.GetActiveJobs()
            .Select(j => new ProcessingJobViewModel(j))
            .ToList();
        
        await InvokeAsync(StateHasChanged);
    }
    
    private void OnStateChanged(object? sender, ProcessingStateChangedEventArgs e)
    {
        InvokeAsync(async () =>
        {
            await RefreshActiveJobs();
        });
    }
    
    public async ValueTask DisposeAsync()
    {
        StateManager.StateChanged -= OnStateChanged;
        refreshTimer?.Dispose();
        
        if (hubConnection != null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

## Testing Strategy

### Unit Testing

```csharp
[TestClass]
public class CameraServiceTests
{
    [TestMethod]
    public async Task StartCamera_WithValidOptions_ReturnsStream()
    {
        // Arrange
        var jsRuntime = new Mock<IJSRuntime>();
        var service = new CameraService(jsRuntime.Object);
        
        // Act
        var result = await service.StartCameraAsync(new CameraOptions());
        
        // Assert
        Assert.IsNotNull(result);
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class OCRServiceIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task ProcessReceipt_WithValidImage_ReturnsData()
    {
        // Arrange
        var client = new OCRServiceClient(new HttpClient());
        var adapter = new OCRServiceAdapter(client);
        var testImage = File.ReadAllBytes("TestData/receipt.jpg");
        
        // Act
        var result = await adapter.ProcessReceiptAsync(testImage);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.MerchantName);
        Assert.IsTrue(result.Total > 0);
    }
}
```

### Component Testing

```csharp
[TestClass]
public class DocumentScannerComponentTests : TestContext
{
    [TestMethod]
    public void Component_RendersCorrectly()
    {
        // Arrange & Act
        var component = RenderComponent<DocumentScannerComponent>();
        
        // Assert
        Assert.IsNotNull(component.Find(".scanner-container"));
        Assert.IsNotNull(component.Find(".camera-view"));
    }
}
```

### E2E Testing Scenarios

1. **Happy Path**
   - User grants camera permission
   - Captures image successfully
   - OCR processes correctly
   - Results displayed

2. **Error Scenarios**
   - Camera permission denied
   - Network failure during OCR
   - Invalid image format
   - OCR service unavailable

3. **Edge Cases**
   - Multiple rapid captures
   - Camera switching during capture
   - Background/foreground transitions
   - Low memory conditions

## Performance Considerations

### Optimization Strategies

1. **Image Optimization**
   ```csharp
   public async Task<byte[]> OptimizeForOCR(byte[] image)
   {
       // Resize to optimal dimensions (max 2048px)
       // Convert to grayscale if color not needed
       // Compress using appropriate quality (85-90%)
       // Remove metadata
   }
   ```

2. **Lazy Loading**
   - Load scanner component on demand
   - Defer JavaScript module loading
   - Progressive enhancement

3. **Caching Strategy**
   ```csharp
   public class OCRResultCache
   {
       private readonly IMemoryCache _cache;
       
       public async Task<T> GetOrProcessAsync<T>(
           string key, 
           Func<Task<T>> factory,
           TimeSpan expiration)
       {
           return await _cache.GetOrCreateAsync(key, async entry =>
           {
               entry.AbsoluteExpirationRelativeToNow = expiration;
               return await factory();
           });
       }
   }
   ```

4. **Bundle Size Optimization**
   - Tree shaking for unused code
   - Code splitting for scanner module
   - WebP format for assets

### Performance Metrics

- **Target Metrics**
  - Camera initialization: < 2 seconds
  - Image capture: < 100ms
  - OCR processing: < 5 seconds
  - Results rendering: < 500ms

- **Monitoring**
  ```csharp
  public class PerformanceMonitor
  {
      public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
      {
          var stopwatch = Stopwatch.StartNew();
          try
          {
              return await action();
          }
          finally
          {
              _telemetry.TrackMetric($"Scanner.{operation}", stopwatch.ElapsedMilliseconds);
          }
      }
  }
  ```

## Implementation Roadmap

### Phase 1: Foundation & Cryptographic Infrastructure (Week 1)
- [ ] Create NoLock.Social.DocumentScanner project
- [ ] Set up project structure and dependencies
- [ ] Implement Ed25519 document signer service
- [ ] Create key management service
- [ ] Design signed document schema
- [ ] Set up secure key storage

### Phase 2: Content-Addressable Storage (Week 1-2)
- [ ] Implement CAS storage service
- [ ] Create hash calculator for SHA-256
- [ ] Build block store for content chunks
- [ ] Add content deduplication logic
- [ ] Implement content indexing
- [ ] Create CAS metadata management

### Phase 3: Async OCR Processing (Week 2-3)
- [ ] Generate OCR service proxy using NSwag
- [ ] Implement adaptive polling service
- [ ] Create background job processor
- [ ] Add OCR status tracking
- [ ] Implement timeout handling (2 minutes)
- [ ] Build retry logic for failed requests
- [ ] Handle duplicate submission caching

### Phase 4: Camera Integration (Week 3)
- [ ] Implement camera service with JavaScript interop
- [ ] Create camera view component
- [ ] Add device selection and switching
- [ ] Implement capture functionality
- [ ] Add permission handling
- [ ] Integrate with document signing

### Phase 5: Document Gallery & Preview (Week 4)
- [ ] Create document gallery component
- [ ] Implement thumbnail generation from CAS
- [ ] Add virtual scrolling for performance
- [ ] Build document preview component
- [ ] Add image viewer with zoom/pan
- [ ] Create OCR text viewer
- [ ] Display signature verification status

### Phase 6: Processing Status & Notifications (Week 4-5)
- [ ] Create processing status component
- [ ] Implement polling progress indicators
- [ ] Add retry controls for failures
- [ ] Build notification system
- [ ] Handle mobile sleep prevention
- [ ] Create timeout warnings

### Phase 7: UI/UX Implementation (Week 5)
- [ ] Implement responsive layouts
- [ ] Create mobile-optimized gallery
- [ ] Add desktop layout variations
- [ ] Remove image edit/crop tools
- [ ] Update navigation flow
- [ ] Add search and filter capabilities

### Phase 8: Integration & Testing (Week 6)
- [ ] Integrate all components
- [ ] Test async OCR workflow end-to-end
- [ ] Verify signature creation and validation
- [ ] Test CAS storage and retrieval
- [ ] Validate polling timeout scenarios
- [ ] Test mobile sleep handling

### Phase 9: Security & Performance (Week 6-7)
- [ ] Security audit of crypto implementation
- [ ] Verify Ed25519 signatures
- [ ] Test key rotation scenarios
- [ ] Optimize CAS performance
- [ ] Implement caching strategies
- [ ] Profile and optimize polling intervals

### Phase 10: Documentation and Deployment (Week 7-8)
- [ ] Document cryptographic architecture
- [ ] Create CAS operation guides
- [ ] Write async processing documentation
- [ ] Set up monitoring for background jobs
- [ ] Deploy to production environment
- [ ] Monitor OCR processing metrics

## Conclusion

This architecture provides a robust, cryptographically secure, and scalable solution for document scanning with asynchronous OCR processing. The implementation addresses critical challenges including long OCR processing delays, mobile device sleep issues, and the need for immediate image availability while OCR processes in the background.

### Key Architectural Decisions

1. **Pluggable Document Processor Architecture**: The plugin-based design enables seamless addition of new document types without modifying the core system. Each processor implements a common interface while providing document-specific logic for validation, parsing, and extraction.

2. **Cryptographic Document Structure**: Ed25519 signed documents ensure integrity and authenticity, with progressive enhancement allowing immediate image access while OCR processing continues asynchronously.

3. **Content-Addressable Storage**: CAS provides deduplication, efficient storage, and immutable content references, essential for maintaining document integrity.

4. **Adaptive Polling Strategy**: Sophisticated polling with exponential backoff prevents mobile devices from sleeping while efficiently managing the 1-2 minute OCR processing window.

5. **Gallery-First UI**: Removal of image editing tools in favor of a browsable gallery provides better user experience for reviewing multiple documents and their OCR results.

### Benefits of the Pluggable Architecture

#### Extensibility
- **Zero Core Modification**: New document types can be added without changing the core processing pipeline
- **Plugin Isolation**: Each processor is independent, preventing changes to one from affecting others
- **Dynamic Registration**: Processors can be loaded at runtime from configuration or discovered via reflection
- **Hot Reload Capability**: Plugins can be updated without system downtime

#### Maintainability
- **Single Responsibility**: Each processor handles exactly one document type
- **Clear Interfaces**: Well-defined contracts between processors and the core system
- **Testability**: Each processor can be tested in isolation
- **Version Management**: Different processor versions can coexist for backward compatibility

#### Scalability
- **Parallel Processing**: Different document types can be processed concurrently
- **Resource Optimization**: Load only the processors that are needed
- **Performance Tuning**: Each processor can be optimized independently
- **Caching Strategy**: Processor-specific caching rules based on document characteristics

#### Business Agility
- **Rapid Feature Delivery**: New document types can be added quickly
- **A/B Testing**: Multiple processor versions can run simultaneously for comparison
- **Customer-Specific Processors**: Custom processors for specific client needs
- **Gradual Rollout**: New processors can be enabled selectively

### Implementation Strategy for New Document Types

When adding a new document type (e.g., Form 1040):

1. **Create Processor Plugin**
   - Implement `IDocumentProcessorPlugin` interface
   - Define validation rules specific to the document
   - Implement extraction logic for form fields
   - Add document-specific transformations

2. **Register in Configuration**
   ```yaml
   - type: Form1040
     enabled: true
     assembly: NoLock.Processors.TaxForms.dll
     className: NoLock.Processors.TaxForms.Form1040Processor
   ```

3. **Deploy Plugin**
   - Drop assembly into plugins folder
   - Update configuration
   - Restart application or trigger hot reload

4. **Automatic Endpoint Creation**
   - System automatically creates `/api/ocr/form1040` endpoint
   - OpenAPI documentation generated automatically
   - Metrics and monitoring enabled by default

### Key Success Factors
- **Plugin Architecture**: Enables rapid addition of new document types
- **Consistent Pipeline**: All documents flow through same processing stages
- **Automatic Discovery**: New processors are discovered and registered automatically
- **Configuration-Driven**: Enable/disable processors via configuration
- **Cryptographically signed documents**: Ed25519 for integrity verification
- **Asynchronous processing**: Handles 2-minute delays gracefully
- **CAS integration**: Efficient, deduplicated storage
- **Progressive enhancement**: Immediate image access while OCR processes
- **Mobile-optimized polling**: Prevents device sleep
- **Background job processing**: Reliable OCR completion

### Security Considerations
- **Plugin Sandboxing**: Processors run in isolated contexts
- **Validation at Every Stage**: Multi-layer validation ensures data integrity
- **Ed25519 signatures**: Prevent document tampering
- **SHA-256 hashing**: Content addressing
- **Secure key storage**: Protected key management
- **Signature verification**: On document retrieval
- **Immutable content storage**: In CAS

### Future Enhancements
- **Machine Learning Integration**: Processors can leverage ML models for better extraction
- **Template Learning**: Automatic template generation from sample documents
- **Multi-Language Support**: Processors for documents in different languages
- **Composite Documents**: Processors that handle multi-page, multi-type documents
- **Workflow Integration**: Processors that trigger business workflows
- **Distributed CAS replication**: For redundancy
- **Blockchain anchoring**: For timestamping
- **Multi-signature support**: For document approval workflows
- **IPFS integration**: For decentralized storage
- **Zero-knowledge proofs**: For privacy-preserving verification
- **Homomorphic encryption**: For processing encrypted documents

---

*Document Version: 3.0*  
*Last Updated: 2025-01-14*  
*Author: System Architecture Team*  
*Major Update: Added Pluggable Document Processor Architecture for extensible document type support*  
*Previous Update (v2.0): Added Cryptographic Document Architecture, Async OCR Processing, and CAS Integration*