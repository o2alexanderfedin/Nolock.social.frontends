using NoLock.Social.Core.Camera.Models;

namespace NoLock.Social.Core.Camera.Interfaces
{
    public interface ICameraService
    {
        // Initialization for Story 1.8
        ValueTask InitializeAsync();
        
        ValueTask<CameraPermissionState> RequestPermission();
        ValueTask<CameraPermissionState> GetPermissionStateAsync();
        ValueTask<CameraStream> StartStreamAsync();
        ValueTask StopStreamAsync();
        ValueTask<CapturedImage> CaptureImageAsync();
        ValueTask<bool> CheckPermissionsAsync();
        
        // Camera Controls for Story 1.5
        ValueTask<bool> ToggleTorchAsync(bool enabled);
        ValueTask<bool> SwitchCameraAsync(string deviceId);
        ValueTask<bool> SetZoomAsync(double zoomLevel);
        ValueTask<double> GetZoomAsync();
        ValueTask<string[]> GetAvailableCamerasAsync();
        ValueTask<bool> IsTorchSupportedAsync();
        ValueTask<bool> IsZoomSupportedAsync();
        
        // Image Quality Validation for Story 1.6
        ValueTask<ImageQualityResult> ValidateImageQualityAsync(CapturedImage capturedImage);
        ValueTask<BlurDetectionResult> DetectBlurAsync(string imageData);
        ValueTask<LightingQualityResult> AssessLightingAsync(string imageData);
        ValueTask<EdgeDetectionResult> DetectDocumentEdgesAsync(string imageData);
        
        // Multi-Page Document Capture for Story 1.7
        ValueTask<string> CreateDocumentSessionAsync();
        ValueTask AddPageToSessionAsync(string sessionId, CapturedImage capturedImage);
        ValueTask<CapturedImage[]> GetSessionPagesAsync(string sessionId);
        ValueTask<DocumentSession> GetDocumentSessionAsync(string sessionId);
        ValueTask RemovePageFromSessionAsync(string sessionId, int pageIndex);
        ValueTask ReorderPagesInSessionAsync(string sessionId, int fromIndex, int toIndex);
        ValueTask ClearDocumentSessionAsync(string sessionId);
        
        // Session Cleanup and Disposal for Story 1.7
        ValueTask DisposeDocumentSessionAsync(string sessionId);
        ValueTask CleanupInactiveSessionsAsync();
        ValueTask<bool> IsSessionActiveAsync(string sessionId);
    }
}