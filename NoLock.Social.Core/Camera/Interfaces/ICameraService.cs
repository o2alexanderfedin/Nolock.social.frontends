using NoLock.Social.Core.Camera.Models;

namespace NoLock.Social.Core.Camera.Interfaces
{
    public interface ICameraService
    {
        // Initialization for Story 1.8
        Task InitializeAsync();
        
        Task<CameraPermissionState> RequestPermission();
        Task<CameraPermissionState> GetPermissionStateAsync();
        Task<CameraStream> StartStreamAsync();
        Task StopStreamAsync();
        Task<CapturedImage> CaptureImageAsync();
        Task<bool> CheckPermissionsAsync();
        
        // Camera Controls for Story 1.5
        Task<bool> ToggleTorchAsync(bool enabled);
        Task<bool> SwitchCameraAsync(string deviceId);
        Task<bool> SetZoomAsync(double zoomLevel);
        Task<double> GetZoomAsync();
        Task<string[]> GetAvailableCamerasAsync();
        Task<bool> IsTorchSupportedAsync();
        Task<bool> IsZoomSupportedAsync();
        
        // Image Quality Validation for Story 1.6
        Task<ImageQualityResult> ValidateImageQualityAsync(CapturedImage capturedImage);
        Task<BlurDetectionResult> DetectBlurAsync(string imageData);
        Task<LightingQualityResult> AssessLightingAsync(string imageData);
        Task<EdgeDetectionResult> DetectDocumentEdgesAsync(string imageData);
        
        // Multi-Page Document Capture for Story 1.7
        Task<string> CreateDocumentSessionAsync();
        Task AddPageToSessionAsync(string sessionId, CapturedImage capturedImage);
        Task<CapturedImage[]> GetSessionPagesAsync(string sessionId);
        Task<DocumentSession> GetDocumentSessionAsync(string sessionId);
        Task RemovePageFromSessionAsync(string sessionId, int pageIndex);
        Task ReorderPagesInSessionAsync(string sessionId, int fromIndex, int toIndex);
        Task ClearDocumentSessionAsync(string sessionId);
        
        // Session Cleanup and Disposal for Story 1.7
        Task DisposeDocumentSessionAsync(string sessionId);
        Task CleanupInactiveSessionsAsync();
        Task<bool> IsSessionActiveAsync(string sessionId);
    }
}