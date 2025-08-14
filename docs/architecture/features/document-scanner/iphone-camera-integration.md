# iPhone Camera Integration for Document Scanning in Blazor WASM

## Document Information
- **Version**: 1.0.0
- **Date**: 2025-01-14
- **Status**: Research Complete
- **Target Platform**: Blazor WebAssembly (.NET 9.0)
- **Related**: [OCR Scanner Architecture](./ocr-scanner-architecture.md)

## Executive Summary

This document outlines the technical approach for using iPhone cameras as document scanners in Blazor WebAssembly applications running in Chrome on desktop. Through Apple's Continuity Camera feature and WebRTC APIs, we can leverage the iPhone's superior camera system for high-quality document capture, suitable for OCR processing of checks, receipts, and other documents.

## Table of Contents

1. [Overview](#overview)
2. [System Requirements](#system-requirements)
3. [Continuity Camera Setup](#continuity-camera-setup)
4. [Browser Compatibility](#browser-compatibility)
5. [Implementation Approaches](#implementation-approaches)
6. [Document Scanning Specifics](#document-scanning-specifics)
7. [Integration with OCR Pipeline](#integration-with-ocr-pipeline)
8. [Troubleshooting](#troubleshooting)
9. [Security Considerations](#security-considerations)
10. [Performance Optimization](#performance-optimization)

## Overview

### Why iPhone Camera for Document Scanning?

iPhone cameras offer significant advantages over traditional webcams for document capture:

- **Superior Image Quality**: 12MP+ sensors vs typical 720p/1080p webcams
- **Advanced Autofocus**: Fast and accurate focus on document text
- **Optical Image Stabilization**: Reduces blur in captured documents
- **Better Low-Light Performance**: Larger sensors capture clearer images in poor lighting
- **Computational Photography**: Apple's image processing enhances document clarity

### How It Works

1. **Continuity Camera** exposes iPhone as a standard camera device to macOS
2. **WebRTC getUserMedia API** accesses the camera from browser
3. **Blazor WASM** uses JavaScript interop to capture images
4. **High-resolution photos** are extracted from video stream for OCR

## System Requirements

### Hardware Requirements

- **Mac Computer**: 
  - macOS Ventura (13.0) or later
  - Mac models from 2018 or later recommended
  
- **iPhone**: 
  - iPhone XR or later (all models from 2018+)
  - iOS 16 or later
  - Rear camera recommended for document scanning

### Software Requirements

- **Browser**: Chrome, Chrome Canary, Brave, or Edge
- **Blazor WebAssembly**: .NET 9.0 or later
- **Network**: Wi-Fi and Bluetooth enabled on both devices

### Account Requirements

- Same Apple Account on both devices
- Two-factor authentication enabled
- iCloud signed in on both devices

## Continuity Camera Setup

### Initial Configuration

1. **Enable Continuity Camera on iPhone**:
   ```
   Settings > General > AirPlay & Continuity > Continuity Camera WebCam
   Toggle ON (enabled by default)
   ```

2. **Mac Setup**:
   - Ensure Wi-Fi and Bluetooth are enabled
   - Sign into same Apple Account
   - Keep devices within 30 feet

3. **iPhone Positioning**:
   - Use iPhone mount or stand
   - Position in landscape orientation
   - Rear camera facing documents
   - 8-12 inches from document surface

### Connection Methods

#### Wireless Connection (Default)
- Automatic detection when requirements met
- May have slight latency
- No cables required

#### USB Connection (Recommended for Reliability)
- Connect iPhone via Lightning/USB-C cable
- Select "Trust This Computer" on iPhone
- More stable connection
- Lower latency

## Browser Compatibility

### Chrome Specific Issues and Solutions

#### Known Issues
- iPhone may not appear in camera list initially
- Intermittent connection drops
- Camera not detected after system sleep

#### Workarounds

1. **FaceTime Activation Method**:
   ```bash
   1. Open FaceTime on Mac
   2. Select iPhone from Video menu
   3. Close FaceTime
   4. Restart Chrome
   5. iPhone should now appear in camera list
   ```

2. **USB Cable Method**:
   ```bash
   1. Connect iPhone via USB
   2. Trust computer on iPhone
   3. Restart Chrome if needed
   4. Select iPhone from camera dropdown
   ```

3. **Chrome Canary/Beta**:
   - Download Chrome Canary for better Continuity Camera support
   - Beta versions have improved compatibility

### Alternative Browsers

- **Brave**: Full Continuity Camera support
- **Microsoft Edge**: Works seamlessly
- **Safari**: Native support (best compatibility)
- **Firefox**: Limited support, not recommended

## Implementation Approaches

### Approach 1: JavaScript Interop with Native WebRTC

#### JavaScript Module (`cameraCapture.js`):

```javascript
// Initialize camera stream with high resolution for documents
export async function initializeDocumentCamera() {
    try {
        const constraints = {
            video: {
                width: { ideal: 4096, min: 1920 },
                height: { ideal: 2160, min: 1080 },
                facingMode: 'environment', // Prefer back camera
                aspectRatio: { ideal: 16/9 }
            },
            audio: false // No audio needed for document scanning
        };
        
        const stream = await navigator.mediaDevices.getUserMedia(constraints);
        const video = document.getElementById('documentVideo');
        video.srcObject = stream;
        
        // Return available cameras for selection
        const devices = await navigator.mediaDevices.enumerateDevices();
        const cameras = devices.filter(device => device.kind === 'videoinput');
        
        return cameras.map(camera => ({
            deviceId: camera.deviceId,
            label: camera.label || `Camera ${camera.deviceId.substring(0, 8)}`
        }));
    } catch (error) {
        console.error('Camera initialization failed:', error);
        throw error;
    }
}

// Capture high-quality still image from video stream
export async function captureDocument() {
    const video = document.getElementById('documentVideo');
    const canvas = document.createElement('canvas');
    
    // Use full resolution of video stream
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    
    const context = canvas.getContext('2d');
    
    // Optional: Apply image corrections
    context.filter = 'contrast(1.1) brightness(1.05)';
    context.drawImage(video, 0, 0);
    
    // Capture as high-quality JPEG
    const imageData = canvas.toDataURL('image/jpeg', 0.95);
    
    // Also return blob for direct upload
    const blob = await new Promise(resolve => {
        canvas.toBlob(resolve, 'image/jpeg', 0.95);
    });
    
    return {
        dataUrl: imageData,
        blob: blob,
        width: canvas.width,
        height: canvas.height,
        timestamp: new Date().toISOString()
    };
}

// Switch between multiple cameras (including iPhone)
export async function switchCamera(deviceId) {
    const video = document.getElementById('documentVideo');
    const stream = video.srcObject;
    
    // Stop current stream
    if (stream) {
        stream.getTracks().forEach(track => track.stop());
    }
    
    // Start new stream with selected camera
    const constraints = {
        video: {
            deviceId: { exact: deviceId },
            width: { ideal: 4096 },
            height: { ideal: 2160 }
        }
    };
    
    const newStream = await navigator.mediaDevices.getUserMedia(constraints);
    video.srcObject = newStream;
}

// Auto-detect document edges (optional enhancement)
export function detectDocumentEdges(imageData) {
    // Placeholder for edge detection algorithm
    // Could integrate with OpenCV.js or similar
    return {
        topLeft: { x: 0, y: 0 },
        topRight: { x: imageData.width, y: 0 },
        bottomLeft: { x: 0, y: imageData.height },
        bottomRight: { x: imageData.width, y: imageData.height }
    };
}
```

#### Blazor Component (`DocumentScanner.razor`):

```razor
@page "/scan-document"
@using Microsoft.JSInterop
@inject IJSRuntime JS
@inject ILogger<DocumentScanner> Logger
@implements IAsyncDisposable

<div class="document-scanner">
    <h3>Document Scanner</h3>
    
    @if (cameras.Any())
    {
        <div class="camera-selector">
            <label>Select Camera:</label>
            <select @onchange="OnCameraChanged">
                @foreach (var camera in cameras)
                {
                    <option value="@camera.DeviceId">@camera.Label</option>
                }
            </select>
        </div>
    }
    
    <div class="video-container">
        <video id="documentVideo" autoplay></video>
        <div class="capture-overlay">
            <div class="document-guide"></div>
        </div>
    </div>
    
    <div class="controls">
        <button @onclick="CaptureDocument" disabled="@isCapturing" class="btn-capture">
            @if (isCapturing)
            {
                <span>Processing...</span>
            }
            else
            {
                <span>📸 Capture Document</span>
            }
        </button>
    </div>
    
    @if (capturedImages.Any())
    {
        <div class="captured-documents">
            <h4>Captured Documents (@capturedImages.Count)</h4>
            <div class="document-grid">
                @foreach (var image in capturedImages)
                {
                    <div class="document-thumbnail">
                        <img src="@image.DataUrl" alt="Captured document" />
                        <div class="document-info">
                            <span>@image.Timestamp.ToString("HH:mm:ss")</span>
                            <span>@image.Width x @image.Height</span>
                        </div>
                        <button @onclick="() => ProcessForOCR(image)">Process OCR</button>
                    </div>
                }
            </div>
        </div>
    }
</div>

@code {
    private IJSObjectReference? cameraModule;
    private List<CameraDevice> cameras = new();
    private List<CapturedDocument> capturedImages = new();
    private bool isCapturing = false;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // Load the camera module
                cameraModule = await JS.InvokeAsync<IJSObjectReference>(
                    "import", "./js/cameraCapture.js");
                
                // Initialize camera and get device list
                var cameraList = await cameraModule.InvokeAsync<CameraDevice[]>(
                    "initializeDocumentCamera");
                cameras = cameraList.ToList();
                
                // Log iPhone detection
                var iphoneCamera = cameras.FirstOrDefault(c => 
                    c.Label.Contains("iPhone", StringComparison.OrdinalIgnoreCase));
                if (iphoneCamera != null)
                {
                    Logger.LogInformation($"iPhone camera detected: {iphoneCamera.Label}");
                }
                
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to initialize camera");
            }
        }
    }
    
    private async Task CaptureDocument()
    {
        if (cameraModule == null) return;
        
        isCapturing = true;
        StateHasChanged();
        
        try
        {
            var capturedData = await cameraModule.InvokeAsync<CapturedDocument>(
                "captureDocument");
            
            capturedData.Id = Guid.NewGuid().ToString();
            capturedData.Timestamp = DateTime.UtcNow;
            
            capturedImages.Add(capturedData);
            
            Logger.LogInformation($"Document captured: {capturedData.Width}x{capturedData.Height}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to capture document");
        }
        finally
        {
            isCapturing = false;
            StateHasChanged();
        }
    }
    
    private async Task OnCameraChanged(ChangeEventArgs e)
    {
        if (cameraModule == null) return;
        
        var deviceId = e.Value?.ToString();
        if (!string.IsNullOrEmpty(deviceId))
        {
            await cameraModule.InvokeVoidAsync("switchCamera", deviceId);
        }
    }
    
    private async Task ProcessForOCR(CapturedDocument document)
    {
        // Send to OCR service
        // This would integrate with your OCR pipeline
        Logger.LogInformation($"Processing document {document.Id} for OCR");
    }
    
    public async ValueTask DisposeAsync()
    {
        if (cameraModule != null)
        {
            await cameraModule.DisposeAsync();
        }
    }
    
    private class CameraDevice
    {
        public string DeviceId { get; set; } = "";
        public string Label { get; set; } = "";
    }
    
    private class CapturedDocument
    {
        public string Id { get; set; } = "";
        public string DataUrl { get; set; } = "";
        public byte[]? Blob { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
```

### Approach 2: Using Blazor.MediaCaptureStreams Library

#### Installation:

```xml
<PackageReference Include="KristofferStrube.Blazor.MediaCaptureStreams" Version="0.2.0" />
```

#### Implementation:

```csharp
@using KristofferStrube.Blazor.MediaCaptureStreams
@inject IMediaDevicesService MediaDevicesService
@inject IMediaStreamService MediaStreamService

@code {
    private MediaDevices? mediaDevices;
    private MediaStream? mediaStream;
    private IEnumerable<MediaDeviceInfo> videoDevices = Array.Empty<MediaDeviceInfo>();
    
    protected override async Task OnInitializedAsync()
    {
        // Get media devices
        mediaDevices = await MediaDevicesService.GetMediaDevicesAsync();
        
        // Request camera permission and get stream
        var constraints = new MediaStreamConstraints
        {
            Video = new VideoConstraints
            {
                Width = new ConstrainULong { Ideal = 4096 },
                Height = new ConstrainULong { Ideal = 2160 },
                FacingMode = VideoFacingMode.Environment
            }
        };
        
        mediaStream = await mediaDevices.GetUserMediaAsync(constraints);
        
        // Enumerate devices after permission granted
        var devices = await mediaDevices.EnumerateDevicesAsync();
        videoDevices = devices.Where(d => d.Kind == MediaDeviceKind.VideoInput);
        
        // Check for iPhone
        var iphone = videoDevices.FirstOrDefault(d => 
            d.Label.Contains("iPhone", StringComparison.OrdinalIgnoreCase));
        if (iphone != null)
        {
            Logger.LogInformation($"iPhone detected: {iphone.Label}");
        }
    }
    
    private async Task<byte[]> CaptureFrame()
    {
        if (mediaStream == null) return Array.Empty<byte>();
        
        // Get video track
        var videoTracks = await mediaStream.GetVideoTracksAsync();
        var track = videoTracks.FirstOrDefault();
        
        if (track != null)
        {
            // Capture frame using ImageCapture API
            var imageCapture = await ImageCaptureService.CreateAsync(track);
            var blob = await imageCapture.TakePhotoAsync();
            
            // Convert blob to byte array
            return await blob.ArrayBufferAsync();
        }
        
        return Array.Empty<byte>();
    }
}
```

## Document Scanning Specifics

### Optimal Camera Settings for Documents

```javascript
const documentConstraints = {
    video: {
        // Resolution
        width: { ideal: 4096, min: 1920 },
        height: { ideal: 2160, min: 1080 },
        
        // Frame rate (lower is fine for documents)
        frameRate: { ideal: 30, max: 30 },
        
        // Aspect ratio for documents
        aspectRatio: { ideal: 1.414 }, // A4 paper ratio
        
        // Camera selection
        facingMode: 'environment', // Back camera
        
        // Auto-focus for documents
        focusMode: 'continuous',
        
        // Exposure for document clarity
        exposureMode: 'continuous',
        whiteBalanceMode: 'continuous'
    }
};
```

### Document Type Specific Settings

#### Receipts
- Higher contrast filter
- Auto-crop to content
- Grayscale conversion option

```javascript
context.filter = 'contrast(1.3) brightness(1.1) grayscale(1)';
```

#### Checks
- Full color capture required
- MICR line preservation
- No aggressive filtering

```javascript
context.filter = 'contrast(1.05)'; // Minimal enhancement
```

#### IDs and Cards
- Fixed aspect ratio capture
- Both sides capture workflow
- High resolution mandatory

### Image Pre-Processing for OCR

```javascript
export function preprocessForOCR(canvas, documentType) {
    const ctx = canvas.getContext('2d');
    
    switch(documentType) {
        case 'receipt':
            // Enhance contrast for faded receipts
            ctx.filter = 'contrast(1.5) brightness(1.2)';
            break;
            
        case 'check':
            // Minimal processing to preserve MICR
            ctx.filter = 'contrast(1.1)';
            break;
            
        case 'id':
            // Sharpen for small text
            ctx.filter = 'contrast(1.2) saturate(0.8)';
            break;
    }
    
    // Redraw with filters
    ctx.drawImage(canvas, 0, 0);
    
    // Optional: Deskew and crop
    // const corrected = deskewDocument(canvas);
    
    return canvas;
}
```

## Integration with OCR Pipeline

### Workflow Integration

```csharp
public class DocumentScannerService : IDocumentScannerService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IContentAddressableStorage _storage;
    private readonly IOcrService _ocrService;
    private readonly ILogger<DocumentScannerService> _logger;
    
    public async Task<ScannedDocument> ScanAndProcessAsync()
    {
        // 1. Capture from iPhone camera
        var imageData = await _jsRuntime.InvokeAsync<CaptureResult>("captureDocument");
        
        // 2. Store in CAS
        var contentRef = await _storage.StoreAsync(imageData.Blob);
        
        // 3. Create signed document
        var document = new SignedDocument
        {
            DocumentId = Guid.NewGuid().ToString(),
            ImageHash = contentRef.Hash,
            CapturedAt = DateTime.UtcNow,
            Metadata = new DocumentMetadata
            {
                DeviceType = "iPhone",
                CaptureMethod = "Continuity Camera",
                Resolution = $"{imageData.Width}x{imageData.Height}"
            }
        };
        
        // 4. Queue for OCR processing
        await _ocrService.QueueDocumentAsync(document);
        
        // 5. Return with tracking ID
        return new ScannedDocument
        {
            DocumentId = document.DocumentId,
            Status = ProcessingStatus.Queued,
            ImageUrl = contentRef.ToDataUrl()
        };
    }
}
```

### OCR Service Integration

```csharp
public class OcrPipelineService
{
    public async Task<OcrResult> ProcessDocumentAsync(SignedDocument document)
    {
        // 1. Retrieve image from CAS
        var imageData = await _storage.RetrieveAsync(document.ImageHash);
        
        // 2. Detect document type
        var documentType = await DetectDocumentTypeAsync(imageData);
        
        // 3. Apply type-specific preprocessing
        var processed = await PreprocessImageAsync(imageData, documentType);
        
        // 4. Send to OCR service
        var ocrResult = await _ocrClient.ProcessImageAsync(processed);
        
        // 5. Store OCR result in CAS
        var ocrHash = await _storage.StoreAsync(ocrResult.ToJson());
        
        // 6. Update document with OCR hash
        document.OcrHash = ocrHash;
        
        return ocrResult;
    }
}
```

## Troubleshooting

### Common Issues and Solutions

#### iPhone Not Appearing in Camera List

**Symptoms**: iPhone not visible in browser camera dropdown

**Solutions**:
1. Check all system requirements met
2. Restart both iPhone and Mac
3. Try USB connection instead of wireless
4. Open FaceTime first, then Chrome
5. Update to latest macOS and iOS
6. Reset Continuity Camera settings

#### Poor Image Quality

**Symptoms**: Blurry or low-resolution captures

**Solutions**:
1. Clean iPhone camera lens
2. Ensure adequate lighting
3. Check video constraints for resolution
4. Stabilize iPhone with mount
5. Adjust distance from document (8-12 inches optimal)

#### Connection Drops

**Symptoms**: Camera freezes or disconnects

**Solutions**:
1. Use USB connection for stability
2. Disable Mac's sleep/screensaver
3. Keep iPhone unlocked during use
4. Check Wi-Fi/Bluetooth interference
5. Restart Continuity Camera service

### Browser Console Diagnostics

```javascript
// Check available cameras
navigator.mediaDevices.enumerateDevices()
    .then(devices => {
        const cameras = devices.filter(d => d.kind === 'videoinput');
        console.log('Available cameras:', cameras);
        
        // Look for iPhone
        const iphone = cameras.find(c => c.label.includes('iPhone'));
        if (iphone) {
            console.log('iPhone found:', iphone);
        } else {
            console.log('iPhone not detected');
        }
    });

// Test camera access
navigator.mediaDevices.getUserMedia({ video: true })
    .then(stream => {
        console.log('Camera access granted');
        console.log('Video tracks:', stream.getVideoTracks());
    })
    .catch(err => {
        console.error('Camera access failed:', err);
    });
```

## Security Considerations

### Permission Management

```csharp
public class CameraPermissionService
{
    private readonly IJSRuntime _jsRuntime;
    
    public async Task<PermissionState> CheckCameraPermissionAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<string>(
                "navigator.permissions.query", 
                new { name = "camera" });
            
            return Enum.Parse<PermissionState>(result);
        }
        catch
        {
            return PermissionState.Prompt;
        }
    }
    
    public async Task<bool> RequestCameraPermissionAsync()
    {
        try
        {
            // This will trigger permission prompt
            await _jsRuntime.InvokeVoidAsync(
                "navigator.mediaDevices.getUserMedia",
                new { video = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

### Data Privacy

1. **Local Processing**: Keep sensitive document processing client-side when possible
2. **Secure Transmission**: Use HTTPS for all data transfers
3. **Temporary Storage**: Clear captured images after processing
4. **User Consent**: Explicit permission for camera access
5. **GDPR Compliance**: Document data handling in privacy policy

### Image Data Sanitization

```javascript
export function sanitizeImageData(imageBlob) {
    // Remove EXIF data that might contain location
    return removeExifData(imageBlob);
}

function removeExifData(blob) {
    return new Promise((resolve) => {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        const img = new Image();
        
        img.onload = () => {
            canvas.width = img.width;
            canvas.height = img.height;
            ctx.drawImage(img, 0, 0);
            
            canvas.toBlob((cleanBlob) => {
                resolve(cleanBlob);
            }, 'image/jpeg', 0.95);
        };
        
        img.src = URL.createObjectURL(blob);
    });
}
```

## Performance Optimization

### Memory Management

```csharp
@implements IAsyncDisposable

@code {
    private readonly List<string> objectUrls = new();
    
    private async Task<string> CreateObjectUrl(byte[] data)
    {
        var url = await JS.InvokeAsync<string>("URL.createObjectURL", data);
        objectUrls.Add(url);
        return url;
    }
    
    public async ValueTask DisposeAsync()
    {
        // Clean up object URLs to prevent memory leaks
        foreach (var url in objectUrls)
        {
            await JS.InvokeVoidAsync("URL.revokeObjectURL", url);
        }
        
        // Stop camera stream
        await JS.InvokeVoidAsync("stopCameraStream");
    }
}
```

### Batch Processing

```csharp
public class BatchDocumentProcessor
{
    private readonly Queue<CapturedDocument> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1);
    
    public async Task QueueDocumentAsync(CapturedDocument document)
    {
        _queue.Enqueue(document);
        
        // Process in batches of 5
        if (_queue.Count >= 5)
        {
            await ProcessBatchAsync();
        }
    }
    
    private async Task ProcessBatchAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var batch = new List<CapturedDocument>();
            while (_queue.Count > 0 && batch.Count < 5)
            {
                batch.Add(_queue.Dequeue());
            }
            
            // Parallel processing
            await Task.WhenAll(batch.Select(ProcessDocumentAsync));
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### Lazy Loading

```razor
@* Only load camera module when needed *@
@if (showScanner)
{
    <DocumentScanner />
}

@code {
    private bool showScanner = false;
    
    private void ShowScanner()
    {
        showScanner = true;
        StateHasChanged();
    }
}
```

## Conclusion

Using iPhone as a document scanner through Continuity Camera in Blazor WASM applications provides a superior alternative to traditional webcams. The combination of high-quality iPhone cameras with WebRTC APIs enables professional-grade document capture suitable for OCR processing.

Key advantages:
- No additional hardware required
- Superior image quality
- Seamless integration with existing web technologies
- Cross-platform compatibility (with caveats)

With proper implementation and the workarounds documented here, this approach can deliver excellent results for document scanning applications.

## References

- [Apple Continuity Camera Documentation](https://support.apple.com/en-us/102546)
- [WebRTC getUserMedia API](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia)
- [Blazor JavaScript Interop](https://docs.microsoft.com/aspnet/core/blazor/javascript-interoperability)
- [Blazor.MediaCaptureStreams Library](https://github.com/KristofferStrube/Blazor.MediaCaptureStreams)