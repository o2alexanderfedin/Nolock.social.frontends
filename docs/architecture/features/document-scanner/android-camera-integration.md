# Android Phone Camera Integration for Document Scanning in Blazor WASM

## Document Information
- **Version**: 1.0.0
- **Date**: 2025-01-14
- **Status**: Research Complete
- **Target Platform**: Blazor WebAssembly (.NET 9.0)
- **Related**: 
  - [OCR Scanner Architecture](./ocr-scanner-architecture.md)
  - [iPhone Camera Integration](./iphone-camera-integration.md)

## Executive Summary

This document outlines multiple approaches for using Android phones as document scanners in Blazor WebAssembly applications. Unlike iOS's unified Continuity Camera, Android offers several methods ranging from native Android 14+ USB webcam support to third-party apps and Microsoft Phone Link integration. All approaches leverage WebRTC APIs for browser integration, making them suitable for high-quality document capture and OCR processing.

## Table of Contents

1. [Overview](#overview)
2. [Integration Methods Comparison](#integration-methods-comparison)
3. [Method 1: Native Android 14+ USB Webcam](#method-1-native-android-14-usb-webcam)
4. [Method 2: Microsoft Phone Link (Windows Only)](#method-2-microsoft-phone-link-windows-only)
5. [Method 3: Third-Party Apps](#method-3-third-party-apps)
6. [Method 4: Browser-Based Solutions](#method-4-browser-based-solutions)
7. [Blazor WASM Implementation](#blazor-wasm-implementation)
8. [Document Scanning Optimization](#document-scanning-optimization)
9. [Cross-Platform Considerations](#cross-platform-considerations)
10. [Troubleshooting](#troubleshooting)

## Overview

### Android Camera Advantages for Document Scanning

Modern Android phones offer excellent cameras for document capture:
- **High Resolution**: Most flagship Android phones have 48MP+ main cameras
- **Advanced AI Processing**: Google's computational photography, Samsung's Scene Optimizer
- **Multiple Lenses**: Wide, ultra-wide, and telephoto options
- **Superior Autofocus**: Phase detection, laser AF, or dual-pixel AF
- **Night Mode**: Better low-light document capture

### Key Differences from iPhone Integration

- **Multiple Methods**: No single unified approach like Apple's Continuity Camera
- **Platform Fragmentation**: Solutions vary by Android version and manufacturer
- **More Flexibility**: Both wired and wireless options available
- **Windows-Centric**: Better native Windows integration than Mac

## Integration Methods Comparison

| Method | Platform Support | Android Version | Connection Type | Quality | Ease of Setup |
|--------|-----------------|-----------------|-----------------|---------|---------------|
| Native Android 14+ | Windows/Mac/Linux | Android 14+ | USB | Native | ⭐⭐⭐⭐⭐ |
| Phone Link | Windows 11 only | Android 9+ | Wireless | 720p-1080p | ⭐⭐⭐⭐ |
| DroidCam | Windows/Mac/Linux | Android 5+ | USB/WiFi | Up to 4K | ⭐⭐⭐ |
| Iriun | Windows/Mac/Linux | Android 5+ | USB/WiFi | Up to 4K | ⭐⭐⭐⭐ |
| IP Webcam | Any (Browser) | Android 4+ | Network | Variable | ⭐⭐ |

## Method 1: Native Android 14+ USB Webcam

### Overview

Starting with Android 14, Google introduced native USB Video Class (UVC) support, allowing Android phones to function as standard webcams without additional software.

### Requirements

- **Android Device**: Android 14 or later
- **Supported Devices** (as of 2024):
  - Google Pixel (all models with Android 14)
  - Motorola phones with Android 14
  - Limited support on Samsung Galaxy and OnePlus (coming soon)
- **Connection**: USB cable (USB-C or micro-USB)
- **Desktop OS**: Windows 10/11, macOS, or Linux

### Setup Process

1. **Enable Developer Options** (if needed):
   ```
   Settings → About Phone → Tap Build Number 7 times
   ```

2. **Connect via USB**:
   - Connect Android phone to computer with USB cable
   - Swipe down notification shade
   - Tap "Charging this device via USB"
   - Select "Webcam" under "Use USB for"

3. **Verify in Browser**:
   ```javascript
   // Check if Android camera appears
   navigator.mediaDevices.enumerateDevices()
     .then(devices => {
       const cameras = devices.filter(d => d.kind === 'videoinput');
       console.log('Available cameras:', cameras);
     });
   ```

### Advantages
- No third-party apps required
- Native OS integration
- Full camera resolution available
- Low latency
- Works across all desktop platforms

### Limitations
- Requires Android 14+
- Limited device support in 2024
- USB connection only
- No wireless option

## Method 2: Microsoft Phone Link (Windows Only)

### Overview

Windows 11's Phone Link app provides wireless Android camera streaming through the "Connected Camera" feature, available in the 2024 Update (24H2).

### Requirements

- **Windows**: Windows 11 24H2 or later
- **Android**: Android 9.0+ with Link to Windows app v1.24012+
- **Network**: Same Wi-Fi network
- **Account**: Same Microsoft account on both devices

### Setup Process

1. **Windows Setup**:
   ```powershell
   # Open Settings
   Settings → Bluetooth & devices → Mobile devices
   
   # Enable Connected Camera
   Manage devices → Allow PC to access Android phone
   Toggle "Use as a connected camera" ON
   ```

2. **Android Setup**:
   - Install "Link to Windows" from Play Store
   - Sign in with Microsoft account
   - Grant necessary permissions

3. **Connect Camera**:
   - Open any video app on Windows
   - Select phone from camera list
   - Tap notification on Android to allow

### Features
- **Wireless Connection**: No cables needed
- **Camera Switching**: Toggle between front/back cameras
- **Stream Control**: Pause/resume capability
- **Effects Support**: Use phone's camera effects

### Blazor Integration
```javascript
// Phone appears as standard webcam
const stream = await navigator.mediaDevices.getUserMedia({
  video: {
    deviceId: { exact: phoneDeviceId },
    width: { ideal: 1920 },
    height: { ideal: 1080 }
  }
});
```

### Limitations
- Windows 11 only
- Maximum 720p-1080p resolution
- Requires stable Wi-Fi
- Slight latency possible

## Method 3: Third-Party Apps

### DroidCam

#### Overview
Popular app with both free and paid versions, supporting USB and wireless connections.

#### Setup
1. **Install DroidCam Client** on PC (Windows/Mac/Linux)
2. **Install DroidCam App** on Android
3. **Connect** via USB or same Wi-Fi network
4. **Enter IP** address shown on phone into PC client

#### Features
- Free version: 640x480 resolution
- Paid version: Up to 1080p (4K with OBS)
- USB and WiFi support
- OBS Studio integration
- Audio support

#### JavaScript Integration
```javascript
// DroidCam appears as "DroidCam Source" in device list
const devices = await navigator.mediaDevices.enumerateDevices();
const droidcam = devices.find(d => 
  d.label.includes('DroidCam') && d.kind === 'videoinput'
);
```

### Iriun Webcam

#### Overview
Free app known for simplicity and high quality, with optional paid features.

#### Setup
1. **Install Iriun** on both PC and Android
2. **Connect** to same Wi-Fi or via USB
3. **Auto-detection** - app automatically connects

#### Features
- Free 4K support (device dependent)
- Automatic connection
- Lower resource usage
- Portrait/landscape modes
- Background replacement (paid)

#### Advantages for Document Scanning
- Better autofocus than DroidCam
- Higher free resolution
- More stable connection
- Cleaner interface

### IP Webcam

#### Overview
Turns Android into network camera accessible via browser.

#### Setup
1. **Install IP Webcam** app on Android
2. **Start Server** in app
3. **Navigate** to provided IP in browser
4. **Use URL** as camera source

#### Browser Integration
```javascript
// Use IP Webcam stream directly
const video = document.getElementById('video');
video.src = 'http://192.168.1.100:8080/video';
```

## Method 4: Browser-Based Solutions

### Direct Browser Access

Some Android browsers support direct camera sharing:

```javascript
// Android Chrome/Firefox camera access
async function setupAndroidCamera() {
  try {
    // Request permission
    const stream = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: { exact: 'environment' }, // Rear camera
        width: { ideal: 4096 },
        height: { ideal: 2160 }
      }
    });
    
    // Check for Android device
    const devices = await navigator.mediaDevices.enumerateDevices();
    const androidCameras = devices.filter(d => 
      d.kind === 'videoinput' && 
      (d.label.includes('facing back') || d.label.includes('rear'))
    );
    
    return { stream, cameras: androidCameras };
  } catch (err) {
    console.error('Camera access failed:', err);
  }
}
```

## Blazor WASM Implementation

### Unified Camera Service

Create a service that works with any Android camera method:

```csharp
using Microsoft.JSInterop;

public interface IAndroidCameraService
{
    Task<CameraDevice[]> GetAvailableCamerasAsync();
    Task<CameraStream> StartCameraAsync(string deviceId);
    Task<CapturedImage> CaptureDocumentAsync();
    Task StopCameraAsync();
}

public class AndroidCameraService : IAndroidCameraService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _cameraModule;
    
    public AndroidCameraService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }
    
    public async Task<CameraDevice[]> GetAvailableCamerasAsync()
    {
        _cameraModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/androidCamera.js");
            
        var devices = await _cameraModule.InvokeAsync<CameraDevice[]>(
            "enumerateCameras");
            
        // Identify Android devices
        return devices.Where(d => IsAndroidCamera(d)).ToArray();
    }
    
    private bool IsAndroidCamera(CameraDevice device)
    {
        // Check for Android camera indicators
        var androidIndicators = new[] {
            "android", "droidcam", "iriun", "ip webcam",
            "phone", "mobile", "rear", "back"
        };
        
        var label = device.Label.ToLower();
        return androidIndicators.Any(indicator => label.Contains(indicator));
    }
    
    public async Task<CapturedImage> CaptureDocumentAsync()
    {
        if (_cameraModule == null)
            throw new InvalidOperationException("Camera not initialized");
            
        var imageData = await _cameraModule.InvokeAsync<CapturedImage>(
            "captureHighResDocument");
            
        // Enhance for document scanning
        if (imageData.Width < 1920)
        {
            // Upscale or request higher resolution
            await _cameraModule.InvokeVoidAsync("requestHigherResolution");
        }
        
        return imageData;
    }
}
```

### JavaScript Module for Android Cameras

```javascript
// androidCamera.js
export async function enumerateCameras() {
    const devices = await navigator.mediaDevices.enumerateDevices();
    const cameras = devices.filter(d => d.kind === 'videoinput');
    
    // Enhance device info for Android
    return cameras.map(camera => ({
        deviceId: camera.deviceId,
        label: camera.label || 'Unknown Camera',
        isAndroid: detectAndroidCamera(camera.label),
        capabilities: await getCameraCapabilities(camera.deviceId)
    }));
}

function detectAndroidCamera(label) {
    const androidPatterns = [
        /android/i, /droidcam/i, /iriun/i,
        /phone/i, /mobile/i, /webcam \d+/i
    ];
    return androidPatterns.some(pattern => pattern.test(label));
}

async function getCameraCapabilities(deviceId) {
    try {
        const stream = await navigator.mediaDevices.getUserMedia({
            video: { deviceId: { exact: deviceId } }
        });
        
        const track = stream.getVideoTracks()[0];
        const capabilities = track.getCapabilities();
        
        // Clean up
        track.stop();
        
        return {
            maxWidth: capabilities.width?.max || 1920,
            maxHeight: capabilities.height?.max || 1080,
            facingMode: capabilities.facingMode || ['unknown']
        };
    } catch {
        return { maxWidth: 1920, maxHeight: 1080 };
    }
}

export async function captureHighResDocument() {
    const video = document.getElementById('androidVideo');
    const canvas = document.createElement('canvas');
    
    // Use maximum resolution
    const videoWidth = video.videoWidth;
    const videoHeight = video.videoHeight;
    
    // For document scanning, we want maximum quality
    canvas.width = Math.min(videoWidth, 4096);
    canvas.height = Math.min(videoHeight, 2160);
    
    const ctx = canvas.getContext('2d');
    
    // Apply Android-specific enhancements
    if (window.androidEnhancements) {
        ctx.filter = 'contrast(1.1) saturate(1.1)';
    }
    
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    
    // High quality JPEG for documents
    const blob = await new Promise(resolve => {
        canvas.toBlob(resolve, 'image/jpeg', 0.98);
    });
    
    return {
        blob: blob,
        dataUrl: canvas.toDataURL('image/jpeg', 0.98),
        width: canvas.width,
        height: canvas.height,
        timestamp: Date.now(),
        deviceInfo: await getDeviceInfo()
    };
}

async function getDeviceInfo() {
    const video = document.getElementById('androidVideo');
    const stream = video.srcObject;
    const track = stream?.getVideoTracks()[0];
    
    if (track) {
        const settings = track.getSettings();
        return {
            deviceId: settings.deviceId,
            width: settings.width,
            height: settings.height,
            facingMode: settings.facingMode
        };
    }
    
    return null;
}
```

### Blazor Component for Android Scanning

```razor
@page "/android-scanner"
@using Microsoft.JSInterop
@inject IAndroidCameraService CameraService
@inject ILogger<AndroidScanner> Logger

<div class="android-scanner">
    <h3>Android Document Scanner</h3>
    
    <div class="connection-status">
        @if (connectionMethod != ConnectionMethod.None)
        {
            <span class="badge badge-success">
                Connected via @connectionMethod
            </span>
        }
    </div>
    
    @if (!cameras.Any())
    {
        <div class="setup-guide">
            <h4>Setup Android Camera</h4>
            <div class="method-selector">
                <button @onclick="() => SelectMethod(ConnectionMethod.Native)"
                        class="btn btn-primary">
                    Native USB (Android 14+)
                </button>
                <button @onclick="() => SelectMethod(ConnectionMethod.PhoneLink)"
                        class="btn btn-primary">
                    Phone Link (Windows)
                </button>
                <button @onclick="() => SelectMethod(ConnectionMethod.ThirdParty)"
                        class="btn btn-primary">
                    Third-Party App
                </button>
            </div>
            
            @if (connectionMethod != ConnectionMethod.None)
            {
                <div class="setup-instructions">
                    @switch (connectionMethod)
                    {
                        case ConnectionMethod.Native:
                            <NativeAndroidSetup OnConnected="OnCameraConnected" />
                            break;
                        case ConnectionMethod.PhoneLink:
                            <PhoneLinkSetup OnConnected="OnCameraConnected" />
                            break;
                        case ConnectionMethod.ThirdParty:
                            <ThirdPartySetup OnConnected="OnCameraConnected" />
                            break;
                    }
                </div>
            }
        </div>
    }
    else
    {
        <div class="camera-controls">
            <select @onchange="OnCameraChanged" class="form-control">
                <option value="">Select Camera</option>
                @foreach (var cam in cameras)
                {
                    <option value="@cam.DeviceId">
                        @cam.Label 
                        @if (cam.IsAndroid) 
                        { 
                            <span>(Android)</span> 
                        }
                    </option>
                }
            </select>
            
            @if (selectedCamera != null)
            {
                <div class="camera-info">
                    Max Resolution: @selectedCamera.Capabilities.MaxWidth x @selectedCamera.Capabilities.MaxHeight
                </div>
            }
        </div>
        
        <div class="video-container">
            <video id="androidVideo" autoplay></video>
            <div class="scan-overlay">
                <div class="document-frame"></div>
                <div class="scan-hint">Position document within frame</div>
            </div>
        </div>
        
        <div class="capture-controls">
            <button @onclick="CaptureDocument" 
                    disabled="@(isCapturing || selectedCamera == null)"
                    class="btn btn-primary btn-lg">
                @if (isCapturing)
                {
                    <span class="spinner-border spinner-border-sm"></span>
                    <span>Processing...</span>
                }
                else
                {
                    <span>📸 Capture Document</span>
                }
            </button>
            
            <div class="capture-options">
                <label>
                    <input type="checkbox" @bind="enhanceImage" />
                    Enhance for OCR
                </label>
                <label>
                    <input type="checkbox" @bind="autoDetectEdges" />
                    Auto-detect edges
                </label>
            </div>
        </div>
        
        @if (capturedDocuments.Any())
        {
            <div class="captured-gallery">
                <h4>Captured Documents</h4>
                <div class="document-grid">
                    @foreach (var doc in capturedDocuments)
                    {
                        <DocumentThumbnail Document="doc" 
                                         OnProcess="ProcessForOCR"
                                         OnRetake="RetakeDocument" />
                    }
                </div>
                
                <button @onclick="ProcessBatch" 
                        disabled="@(!capturedDocuments.Any())"
                        class="btn btn-success">
                    Process All for OCR (@capturedDocuments.Count)
                </button>
            </div>
        }
    }
</div>

<style>
    .android-scanner {
        max-width: 1200px;
        margin: 0 auto;
        padding: 20px;
    }
    
    .video-container {
        position: relative;
        width: 100%;
        max-width: 800px;
        margin: 20px auto;
    }
    
    #androidVideo {
        width: 100%;
        height: auto;
        border: 2px solid #ccc;
        border-radius: 8px;
    }
    
    .scan-overlay {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        pointer-events: none;
    }
    
    .document-frame {
        position: absolute;
        top: 10%;
        left: 10%;
        right: 10%;
        bottom: 10%;
        border: 3px dashed rgba(0, 123, 255, 0.5);
        border-radius: 8px;
    }
    
    .scan-hint {
        position: absolute;
        bottom: 20px;
        left: 50%;
        transform: translateX(-50%);
        background: rgba(0, 0, 0, 0.7);
        color: white;
        padding: 10px 20px;
        border-radius: 20px;
    }
    
    .document-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
        gap: 15px;
        margin: 20px 0;
    }
    
    .method-selector {
        display: flex;
        gap: 10px;
        margin: 20px 0;
    }
    
    .camera-info {
        font-size: 0.9em;
        color: #666;
        margin-top: 5px;
    }
</style>

@code {
    private enum ConnectionMethod
    {
        None,
        Native,
        PhoneLink,
        ThirdParty
    }
    
    private ConnectionMethod connectionMethod = ConnectionMethod.None;
    private List<CameraDevice> cameras = new();
    private CameraDevice? selectedCamera;
    private List<CapturedDocument> capturedDocuments = new();
    private bool isCapturing = false;
    private bool enhanceImage = true;
    private bool autoDetectEdges = false;
    
    protected override async Task OnInitializedAsync()
    {
        // Try to detect available cameras
        await DetectCameras();
    }
    
    private async Task DetectCameras()
    {
        try
        {
            cameras = (await CameraService.GetAvailableCamerasAsync()).ToList();
            
            if (cameras.Any())
            {
                // Auto-select Android camera if found
                selectedCamera = cameras.FirstOrDefault(c => c.IsAndroid) 
                                ?? cameras.First();
                                
                Logger.LogInformation($"Detected {cameras.Count} cameras, " +
                                     $"{cameras.Count(c => c.IsAndroid)} Android");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to detect cameras");
        }
    }
    
    private void SelectMethod(ConnectionMethod method)
    {
        connectionMethod = method;
        StateHasChanged();
    }
    
    private async Task OnCameraConnected()
    {
        // Refresh camera list after connection
        await DetectCameras();
        StateHasChanged();
    }
    
    private async Task OnCameraChanged(ChangeEventArgs e)
    {
        var deviceId = e.Value?.ToString();
        if (!string.IsNullOrEmpty(deviceId))
        {
            selectedCamera = cameras.FirstOrDefault(c => c.DeviceId == deviceId);
            if (selectedCamera != null)
            {
                await CameraService.StartCameraAsync(deviceId);
            }
        }
    }
    
    private async Task CaptureDocument()
    {
        if (selectedCamera == null) return;
        
        isCapturing = true;
        StateHasChanged();
        
        try
        {
            var image = await CameraService.CaptureDocumentAsync();
            
            if (enhanceImage)
            {
                // Apply OCR enhancements
                image = await EnhanceForOCR(image);
            }
            
            if (autoDetectEdges)
            {
                // Detect and crop to document edges
                image = await AutoCropDocument(image);
            }
            
            capturedDocuments.Add(new CapturedDocument
            {
                Id = Guid.NewGuid().ToString(),
                Image = image,
                CapturedAt = DateTime.UtcNow,
                DeviceName = selectedCamera.Label,
                ConnectionMethod = connectionMethod.ToString()
            });
            
            Logger.LogInformation($"Captured document from {selectedCamera.Label}");
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
    
    private async Task<CapturedImage> EnhanceForOCR(CapturedImage image)
    {
        // Implement image enhancement for better OCR
        // This could include contrast adjustment, sharpening, etc.
        return image;
    }
    
    private async Task<CapturedImage> AutoCropDocument(CapturedImage image)
    {
        // Implement edge detection and auto-cropping
        return image;
    }
    
    private async Task ProcessForOCR(CapturedDocument document)
    {
        // Send to OCR service
        Logger.LogInformation($"Processing document {document.Id} for OCR");
    }
    
    private async Task RetakeDocument(CapturedDocument document)
    {
        capturedDocuments.Remove(document);
        StateHasChanged();
    }
    
    private async Task ProcessBatch()
    {
        // Process all captured documents
        foreach (var doc in capturedDocuments)
        {
            await ProcessForOCR(doc);
        }
    }
    
    public class CameraDevice
    {
        public string DeviceId { get; set; } = "";
        public string Label { get; set; } = "";
        public bool IsAndroid { get; set; }
        public CameraCapabilities Capabilities { get; set; } = new();
    }
    
    public class CameraCapabilities
    {
        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }
        public string[] FacingMode { get; set; } = Array.Empty<string>();
    }
    
    public class CapturedDocument
    {
        public string Id { get; set; } = "";
        public CapturedImage Image { get; set; } = new();
        public DateTime CapturedAt { get; set; }
        public string DeviceName { get; set; } = "";
        public string ConnectionMethod { get; set; } = "";
    }
    
    public class CapturedImage
    {
        public byte[] Blob { get; set; } = Array.Empty<byte>();
        public string DataUrl { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
```

## Document Scanning Optimization

### Android-Specific Camera Settings

```javascript
// Optimal settings for different Android cameras
const androidCameraConstraints = {
    // High-end Android (Samsung Galaxy S24, Pixel 8 Pro)
    premium: {
        video: {
            width: { ideal: 4096, min: 3840 },
            height: { ideal: 2160, min: 2160 },
            facingMode: 'environment',
            // Android-specific constraints
            exposureMode: 'continuous',
            focusMode: 'continuous',
            whiteBalanceMode: 'continuous',
            // Frame rate for documents
            frameRate: { ideal: 30, max: 30 }
        }
    },
    
    // Mid-range Android
    standard: {
        video: {
            width: { ideal: 1920 },
            height: { ideal: 1080 },
            facingMode: 'environment'
        }
    },
    
    // Older/Budget Android
    basic: {
        video: {
            width: { ideal: 1280 },
            height: { ideal: 720 },
            facingMode: 'environment'
        }
    }
};

// Auto-detect and apply best settings
async function getOptimalAndroidSettings() {
    const stream = await navigator.mediaDevices.getUserMedia({ video: true });
    const track = stream.getVideoTracks()[0];
    const capabilities = track.getCapabilities();
    track.stop();
    
    if (capabilities.width?.max >= 3840) {
        return androidCameraConstraints.premium;
    } else if (capabilities.width?.max >= 1920) {
        return androidCameraConstraints.standard;
    } else {
        return androidCameraConstraints.basic;
    }
}
```

### Image Processing for Android Cameras

```javascript
// Android cameras often need different processing
export function processAndroidImage(canvas, deviceInfo) {
    const ctx = canvas.getContext('2d');
    
    // Device-specific adjustments
    if (deviceInfo.manufacturer === 'Samsung') {
        // Samsung tends to oversaturate
        ctx.filter = 'saturate(0.9) contrast(1.1)';
    } else if (deviceInfo.manufacturer === 'Google') {
        // Pixel phones have good default processing
        ctx.filter = 'contrast(1.05)';
    } else if (deviceInfo.manufacturer === 'Xiaomi') {
        // Some Xiaomi phones need brightness adjustment
        ctx.filter = 'brightness(1.1) contrast(1.1)';
    } else {
        // Generic Android enhancement
        ctx.filter = 'contrast(1.1) brightness(1.05)';
    }
    
    // Redraw with adjustments
    const tempCanvas = document.createElement('canvas');
    tempCanvas.width = canvas.width;
    tempCanvas.height = canvas.height;
    const tempCtx = tempCanvas.getContext('2d');
    tempCtx.drawImage(canvas, 0, 0);
    
    ctx.drawImage(tempCanvas, 0, 0);
    
    return canvas;
}
```

## Cross-Platform Considerations

### Platform Detection and Method Selection

```csharp
public class PlatformCameraService
{
    private readonly IJSRuntime _jsRuntime;
    
    public async Task<CameraMethod> GetBestCameraMethod()
    {
        var platform = await GetPlatform();
        var androidVersion = await GetAndroidVersion();
        
        if (platform == "Windows")
        {
            // Check for Windows 11 24H2
            var windowsVersion = await GetWindowsVersion();
            if (windowsVersion >= "24H2")
            {
                return CameraMethod.PhoneLink;
            }
        }
        
        if (androidVersion >= 14)
        {
            return CameraMethod.NativeUSB;
        }
        
        // Fallback to third-party apps
        return CameraMethod.ThirdParty;
    }
    
    private async Task<string> GetPlatform()
    {
        return await _jsRuntime.InvokeAsync<string>(
            "eval", "navigator.platform");
    }
    
    private async Task<int> GetAndroidVersion()
    {
        // This would need to be communicated from the Android app
        // or detected through the camera label
        return 13; // Default fallback
    }
}
```

### Universal Camera Interface

```csharp
public interface IUniversalCameraService
{
    Task<bool> IsAndroidCameraAvailable();
    Task<bool> IsIPhoneCameraAvailable();
    Task<CameraDevice> GetBestAvailableCamera();
    Task<CapturedImage> CaptureDocumentAsync(CameraDevice camera);
}

public class UniversalCameraService : IUniversalCameraService
{
    public async Task<CameraDevice> GetBestAvailableCamera()
    {
        var devices = await EnumerateAllCameras();
        
        // Priority order for document scanning
        var priorities = new[] {
            "iPhone",           // Best quality on Mac
            "Pixel",           // Google computational photography
            "Galaxy",          // Samsung high-res sensors
            "DroidCam",        // Reliable third-party
            "Iriun",           // Good quality third-party
            "Integrated"       // Fallback to built-in
        };
        
        foreach (var priority in priorities)
        {
            var device = devices.FirstOrDefault(d => 
                d.Label.Contains(priority, StringComparison.OrdinalIgnoreCase));
            if (device != null)
                return device;
        }
        
        return devices.FirstOrDefault() ?? throw new Exception("No camera found");
    }
}
```

## Troubleshooting

### Common Android Camera Issues

#### Issue: Android Phone Not Detected

**Solutions**:
1. **USB Debugging**: Enable in Developer Options
2. **USB Configuration**: Try different USB modes (File Transfer, PTP, etc.)
3. **Driver Issues**: Install Android USB drivers on Windows
4. **Cable Quality**: Use original or high-quality USB cable

#### Issue: Low Resolution from Third-Party Apps

**Solutions**:
1. **App Settings**: Check for HD/4K options in app settings
2. **Network Bandwidth**: For WiFi apps, ensure good connection
3. **Paid Version**: Consider upgrading for higher resolution
4. **Alternative App**: Try different apps (Iriun often has better free resolution)

#### Issue: Connection Drops with Phone Link

**Solutions**:
1. **Wi-Fi Stability**: Use 5GHz network if available
2. **Power Saving**: Disable battery optimization for Link to Windows
3. **Bluetooth**: Ensure Bluetooth is enabled and stable
4. **Proximity**: Keep devices within 30 feet

### Diagnostic Commands

```javascript
// Check for Android cameras in browser
async function diagnoseAndroidCamera() {
    console.log('=== Android Camera Diagnostics ===');
    
    // 1. Check permissions
    try {
        const permissionStatus = await navigator.permissions.query({ name: 'camera' });
        console.log('Camera permission:', permissionStatus.state);
    } catch (e) {
        console.log('Permission API not available');
    }
    
    // 2. Enumerate devices
    const devices = await navigator.mediaDevices.enumerateDevices();
    const cameras = devices.filter(d => d.kind === 'videoinput');
    console.log('Available cameras:', cameras.length);
    cameras.forEach((cam, i) => {
        console.log(`Camera ${i + 1}: ${cam.label || 'Unnamed'}`);
        console.log(`  Device ID: ${cam.deviceId}`);
    });
    
    // 3. Test each camera
    for (const camera of cameras) {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({
                video: { deviceId: { exact: camera.deviceId } }
            });
            const track = stream.getVideoTracks()[0];
            const settings = track.getSettings();
            console.log(`Camera ${camera.label} settings:`, settings);
            track.stop();
        } catch (e) {
            console.error(`Failed to access ${camera.label}:`, e);
        }
    }
    
    // 4. Check for specific Android cameras
    const androidApps = ['DroidCam', 'Iriun', 'IP Webcam', 'Phone'];
    androidApps.forEach(app => {
        const found = cameras.find(c => c.label?.includes(app));
        console.log(`${app}: ${found ? 'Found' : 'Not found'}`);
    });
}

// Run diagnostics
diagnoseAndroidCamera();
```

## Security Considerations

### Android-Specific Security

1. **USB Debugging Risks**: Only enable when needed
2. **Network Security**: Use encrypted connections for WiFi apps
3. **App Permissions**: Review third-party app permissions carefully
4. **Data Privacy**: Ensure apps don't upload images without consent

### Secure Implementation

```csharp
public class SecureAndroidCameraService
{
    private readonly IEncryptionService _encryption;
    
    public async Task<SecureImage> CaptureSecureDocument()
    {
        // Capture image
        var image = await CaptureDocument();
        
        // Remove metadata
        image = RemoveExifData(image);
        
        // Encrypt if storing
        if (RequiresStorage)
        {
            image = await _encryption.EncryptImage(image);
        }
        
        // Clear sensitive data from memory
        ClearSensitiveData();
        
        return image;
    }
    
    private void RemoveExifData(byte[] imageData)
    {
        // Strip EXIF data that might contain location or device info
        // Implementation depends on image library used
    }
}
```

## Performance Considerations

### Android Memory Management

```javascript
// Android devices vary greatly in available memory
class AndroidMemoryManager {
    constructor() {
        this.maxImages = this.detectMaxImages();
        this.images = [];
    }
    
    detectMaxImages() {
        // Estimate based on available memory
        if ('memory' in navigator) {
            const memoryGB = (navigator.memory.jsHeapSizeLimit / 1024 / 1024 / 1024);
            if (memoryGB > 4) return 20;  // High-end device
            if (memoryGB > 2) return 10;  // Mid-range
            return 5;  // Budget device
        }
        return 10; // Default
    }
    
    addImage(imageData) {
        if (this.images.length >= this.maxImages) {
            // Remove oldest image
            const removed = this.images.shift();
            URL.revokeObjectURL(removed.url);
        }
        
        this.images.push(imageData);
    }
    
    cleanup() {
        this.images.forEach(img => URL.revokeObjectURL(img.url));
        this.images = [];
    }
}
```

### Batch Processing Optimization

```csharp
public class AndroidBatchProcessor
{
    private readonly int _batchSize;
    
    public AndroidBatchProcessor()
    {
        // Adjust batch size based on connection method
        _batchSize = DetermineBatchSize();
    }
    
    private int DetermineBatchSize()
    {
        return ConnectionMethod switch
        {
            "USB" => 10,      // Fast, stable connection
            "WiFi" => 5,      // Moderate speed
            "PhoneLink" => 3, // Wireless, may have latency
            _ => 5
        };
    }
    
    public async Task ProcessBatch(List<CapturedImage> images)
    {
        var batches = images.Chunk(_batchSize);
        
        foreach (var batch in batches)
        {
            await Task.WhenAll(batch.Select(ProcessImage));
            
            // Brief pause between batches to avoid overload
            await Task.Delay(500);
        }
    }
}
```

## Conclusion

Android phone cameras provide excellent alternatives for document scanning in Blazor WASM applications. With multiple integration methods available—from native Android 14+ USB support to Microsoft Phone Link and third-party apps—developers can choose the approach that best fits their requirements and constraints.

Key takeaways:
- **Native Android 14+ USB**: Simplest setup, best performance
- **Phone Link**: Best for Windows 11 users, wireless convenience
- **Third-party Apps**: Most flexible, work with older Android versions
- **Browser-based**: Direct integration, platform-independent

The variety of options ensures that virtually any Android device can be used as a high-quality document scanner, providing flexibility that matches or exceeds iPhone integration capabilities.

## References

- [Android USB Video Class Documentation](https://source.android.com/docs/core/camera/usb-video-class)
- [Windows Phone Link Documentation](https://support.microsoft.com/en-us/topic/phone-link-requirements-and-setup)
- [WebRTC getUserMedia API](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia)
- [DroidCam Official Site](https://droidcam.app/)
- [Iriun Webcam](https://iriun.com/)
- [IP Webcam Documentation](https://ip-webcam.appspot.com/)