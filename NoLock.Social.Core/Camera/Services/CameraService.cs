using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage.Interfaces;
using System.Text.Json;

namespace NoLock.Social.Core.Camera.Services
{
    public class CameraService : ICameraService, IDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IOfflineStorageService _offlineStorage;
        private readonly IOfflineQueueService _offlineQueue;
        private readonly IConnectivityService _connectivity;
        private CameraPermissionState _currentPermissionState = CameraPermissionState.Prompt;
        private CameraStream _currentStream = null;
        private CameraControlSettings _controlSettings = new CameraControlSettings();
        private readonly Dictionary<string, DocumentSession> _activeSessions = new();
        private bool _disposed = false;

        public CameraService(
            IJSRuntime jsRuntime,
            IOfflineStorageService offlineStorage,
            IOfflineQueueService offlineQueue,
            IConnectivityService connectivity)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _offlineStorage = offlineStorage ?? throw new ArgumentNullException(nameof(offlineStorage));
            _offlineQueue = offlineQueue ?? throw new ArgumentNullException(nameof(offlineQueue));
            _connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
        }

        public async Task InitializeAsync()
        {
            ThrowIfDisposed();
            
            try
            {
                // Load all sessions from IndexedDB
                var sessions = await _offlineStorage.GetAllSessionsAsync();
                
                foreach (var session in sessions)
                {
                    if (session != null && !string.IsNullOrEmpty(session.SessionId))
                    {
                        _activeSessions[session.SessionId] = session;
                    }
                }
                
                // Trigger processing of pending operations
                await _offlineQueue.ProcessQueueAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - initialization should be resilient
                // The application should continue to work even if some data can't be restored
                Console.WriteLine($"Warning: Failed to restore some session data during initialization: {ex.Message}");
                
                // Clear any corrupted data that might have been partially loaded
                var corruptedSessionIds = _activeSessions
                    .Where(kvp => kvp.Value == null || string.IsNullOrEmpty(kvp.Value.SessionId))
                    .Select(kvp => kvp.Key)
                    .ToList();
                    
                foreach (var corruptedId in corruptedSessionIds)
                {
                    _activeSessions.Remove(corruptedId);
                }
            }
        }

        public async Task<CameraPermissionState> GetPermissionStateAsync()
        {
            // Call JavaScript to check current camera permission state
            var result = await _jsRuntime.InvokeAsync<string>("cameraPermissions.getState");
            
            // Parse result string to CameraPermissionState enum
            _currentPermissionState = result?.ToLowerInvariant() switch
            {
                "granted" => CameraPermissionState.Granted,
                "denied" => CameraPermissionState.Denied,
                "prompt" => CameraPermissionState.Prompt,
                "not-requested" => CameraPermissionState.NotRequested,
                _ => CameraPermissionState.NotRequested
            };
            
            return _currentPermissionState;
        }

        public async Task<CameraPermissionState> RequestPermission()
        {
            // Call JavaScript to request camera permission
            var result = await _jsRuntime.InvokeAsync<string>("cameraPermissions.request");
            
            // Parse result string to CameraPermissionState enum
            _currentPermissionState = result?.ToLowerInvariant() switch
            {
                "granted" => CameraPermissionState.Granted,
                "denied" => CameraPermissionState.Denied,
                "prompt" => CameraPermissionState.Prompt,
                _ => CameraPermissionState.Denied
            };
            
            return _currentPermissionState;
        }

        public async Task<CameraStream> StartStreamAsync()
        {
            // Check if stream is already active
            if (_currentStream != null)
            {
                return _currentStream;
            }

            // Check permissions first
            var permissionState = await GetPermissionStateAsync();
            if (permissionState != CameraPermissionState.Granted)
            {
                throw new InvalidOperationException($"Camera permission not granted. Current state: {permissionState}");
            }

            // Call JavaScript to start camera stream
            var streamData = await _jsRuntime.InvokeAsync<dynamic>("camera.startStream");
            
            // Get stream URL and metadata from JavaScript response
            string streamUrl = streamData?.url ?? throw new InvalidOperationException("Failed to get stream URL");
            string streamId = streamData?.id ?? Guid.NewGuid().ToString();
            
            // Create CameraStream object
            _currentStream = new CameraStream
            {
                StreamId = streamId,
                StreamUrl = streamUrl,
                IsActive = true,
                StartedAt = DateTime.UtcNow
            };
            
            // Return the stream
            return _currentStream;
        }

        public async Task StopStreamAsync()
        {
            // Check if there is an active stream to stop
            if (_currentStream == null)
            {
                // No stream active, nothing to stop
                return;
            }

            try
            {
                // Call JavaScript to stop the camera stream
                await _jsRuntime.InvokeVoidAsync("camera.stopStream", _currentStream.StreamId);
                
                // Clear the current stream reference
                _currentStream = null;
            }
            catch (JSException ex)
            {
                // Log the error but don't throw - stream might already be stopped
                Console.WriteLine($"Error stopping camera stream: {ex.Message}");
                
                // Clear the stream reference anyway
                _currentStream = null;
            }
        }

        public async Task<CapturedImage> CaptureImageAsync()
        {
            // Check if there is an active stream
            if (_currentStream == null || !_currentStream.IsActive)
            {
                throw new InvalidOperationException("No active camera stream available for capture");
            }

            // Call JavaScript to capture image from video stream
            var captureData = await _jsRuntime.InvokeAsync<dynamic>("camera.captureImage", _currentStream.StreamId);
            
            // Extract data from JavaScript response
            string imageData = captureData?.imageData ?? throw new InvalidOperationException("Failed to capture image data");
            string imageUrl = captureData?.imageUrl ?? throw new InvalidOperationException("Failed to get image URL");
            int width = captureData?.width ?? 0;
            int height = captureData?.height ?? 0;
            int quality = captureData?.quality ?? 0;
            
            // Create CapturedImage object
            var capturedImage = new CapturedImage
            {
                ImageData = imageData,
                ImageUrl = imageUrl,
                Timestamp = DateTime.UtcNow,
                Width = width,
                Height = height,
                Quality = quality
            };
            
            // Save image to offline storage (new functionality)
            try
            {
                await _offlineStorage.SaveImageAsync(capturedImage);
            }
            catch (OfflineStorageException ex)
            {
                // Log the error but continue - image is still captured
                Console.WriteLine($"Warning: Failed to save captured image to offline storage: {ex.Message}");
            }
            
            return capturedImage;
        }

        public async Task<bool> CheckPermissionsAsync()
        {
            // Get current permission state
            var permissionState = await GetPermissionStateAsync();
            
            // Return true only if permission is explicitly granted
            return permissionState == CameraPermissionState.Granted;
        }

        public async Task<bool> ToggleTorchAsync(bool enabled)
        {
            try
            {
                // Check if torch is supported before attempting to toggle
                if (!await IsTorchSupportedAsync())
                {
                    return false;
                }

                // Call JavaScript to toggle torch/flash
                var result = await _jsRuntime.InvokeAsync<bool>("camera.setTorch", enabled);
                
                // Update settings state if successful
                if (result)
                {
                    _controlSettings.IsTorchEnabled = enabled;
                }
                
                return result;
            }
            catch (JSException)
            {
                // Torch control failed, return false
                return false;
            }
        }

        public async Task<bool> IsTorchSupportedAsync()
        {
            try
            {
                // Call JavaScript to check torch support
                var supported = await _jsRuntime.InvokeAsync<bool>("camera.getTorchSupport");
                
                // Update settings state
                _controlSettings.HasTorchSupport = supported;
                
                return supported;
            }
            catch (JSException)
            {
                // If JavaScript call fails, assume no torch support
                _controlSettings.HasTorchSupport = false;
                return false;
            }
        }

        // Placeholder implementations for remaining interface methods
        public async Task<bool> SwitchCameraAsync(string deviceId)
        {
            try
            {
                // Stop current stream before switching
                await StopStreamAsync();
                
                // Call JavaScript to switch to specified camera device
                var result = await _jsRuntime.InvokeAsync<bool>("camera.switchCamera", deviceId);
                
                // If switch was successful, update control settings with new camera ID
                if (result)
                {
                    _controlSettings.CurrentCameraId = deviceId;
                    
                    // Start new stream with switched camera
                    await StartStreamAsync();
                }
                
                return result;
            }
            catch (JSException)
            {
                // Camera switch failed, return false
                return false;
            }
        }

        public async Task<bool> SetZoomAsync(double zoomLevel)
        {
            try
            {
                // Validate zoom level range
                if (zoomLevel < 1.0)
                {
                    return false;
                }

                // Get maximum zoom to validate upper bound
                var maxZoom = await GetMaxZoomAsync();
                if (zoomLevel > maxZoom)
                {
                    return false;
                }

                // Call JavaScript to set zoom level
                var result = await _jsRuntime.InvokeAsync<bool>("camera.setZoom", zoomLevel);
                
                // Update settings state if successful
                if (result)
                {
                    _controlSettings.ZoomLevel = zoomLevel;
                }
                
                return result;
            }
            catch (JSException)
            {
                // Zoom control failed, return false
                return false;
            }
        }

        public async Task<double> GetZoomAsync()
        {
            try
            {
                // Call JavaScript to get current zoom level
                var zoomLevel = await _jsRuntime.InvokeAsync<double>("camera.getZoom");
                
                // Update settings state
                _controlSettings.ZoomLevel = zoomLevel;
                
                return zoomLevel;
            }
            catch (JSException)
            {
                // If getting zoom fails, return default zoom of 1.0
                return 1.0;
            }
        }

        public async Task<string[]> GetAvailableCamerasAsync()
        {
            try
            {
                // Call JavaScript to enumerate available camera devices
                var devices = await _jsRuntime.InvokeAsync<string[]>("camera.getAvailableCameras");
                
                return devices ?? new string[0];
            }
            catch (JSException)
            {
                // If enumeration fails, return empty array
                return new string[0];
            }
        }

        public async Task<bool> IsZoomSupportedAsync()
        {
            try
            {
                // Call JavaScript to check zoom support
                var supported = await _jsRuntime.InvokeAsync<bool>("camera.getZoomSupport");
                
                // Update settings state
                _controlSettings.HasZoomSupport = supported;
                
                return supported;
            }
            catch (JSException)
            {
                // If JavaScript call fails, assume no zoom support
                _controlSettings.HasZoomSupport = false;
                return false;
            }
        }

        public async Task<double> GetMaxZoomAsync()
        {
            try
            {
                // Call JavaScript to get maximum zoom level
                var maxZoom = await _jsRuntime.InvokeAsync<double>("camera.getMaxZoom");
                
                // Update settings state
                _controlSettings.MaxZoomLevel = maxZoom;
                
                return maxZoom;
            }
            catch (JSException)
            {
                // If getting max zoom fails, return default max of 3.0
                return 3.0;
            }
        }

        public async Task<ImageQualityResult> ValidateImageQualityAsync(CapturedImage capturedImage)
        {
            try
            {
                // Validate input
                if (capturedImage == null || string.IsNullOrEmpty(capturedImage.ImageData))
                {
                    throw new ArgumentException("Invalid captured image data");
                }

                // Perform individual quality analyses
                var blurResult = await DetectBlurAsync(capturedImage.ImageData);
                var lightingResult = await AssessLightingAsync(capturedImage.ImageData);
                var edgeResult = await DetectDocumentEdgesAsync(capturedImage.ImageData);

                // Calculate overall score (weighted average)
                var overallScore = (int)((blurResult.BlurScore * 0.4 + 
                                        lightingResult.LightingScore * 0.3 + 
                                        edgeResult.EdgeScore * 0.3) * 100);

                // Create result with combined analysis
                var result = new ImageQualityResult
                {
                    OverallScore = overallScore,
                    BlurScore = blurResult.BlurScore,
                    LightingScore = lightingResult.LightingScore,
                    EdgeDetectionScore = edgeResult.EdgeScore
                };

                // Add intelligent suggestions based on specific quality scores
                var hasBlurIssue = blurResult.IsBlurry;
                var hasLightingIssue = !lightingResult.IsAdequate;
                var hasEdgeIssue = !edgeResult.HasClearEdges;

                if (hasBlurIssue)
                {
                    result.Issues.Add("Image appears blurry");
                    
                    // Specific blur suggestions based on score severity
                    if (blurResult.BlurScore < 0.3)
                    {
                        result.Suggestions.Add("Hold device much steadier - significant motion detected");
                    }
                    else if (blurResult.BlurScore < 0.4)
                    {
                        result.Suggestions.Add("Move closer to document for better focus");
                    }
                    else
                    {
                        result.Suggestions.Add("Hold camera steadier and tap to focus");
                    }
                }

                if (hasLightingIssue)
                {
                    result.Issues.Add("Poor lighting conditions");
                    
                    // Specific lighting suggestions based on brightness and score
                    if (lightingResult.Brightness < 100)
                    {
                        result.Suggestions.Add("Move to brighter location or enable torch/flash");
                    }
                    else if (lightingResult.Contrast < 0.4)
                    {
                        result.Suggestions.Add("Avoid shadows and ensure even lighting");
                    }
                    else
                    {
                        result.Suggestions.Add("Improve lighting conditions for better clarity");
                    }
                }

                if (hasEdgeIssue)
                {
                    result.Issues.Add("Document edges not clearly detected");
                    
                    // Specific edge detection suggestions based on score and confidence
                    if (edgeResult.Confidence < 0.4)
                    {
                        result.Suggestions.Add("Ensure document is flat and well-positioned");
                    }
                    else if (edgeResult.EdgeScore < 0.5)
                    {
                        result.Suggestions.Add("Position document fully within frame boundaries");
                    }
                    else
                    {
                        result.Suggestions.Add("Move camera to capture complete document outline");
                    }
                }

                // Add combination suggestions for multiple issues
                if (hasBlurIssue && hasLightingIssue)
                {
                    result.Suggestions.Add("Use tripod or stable surface in well-lit area");
                }
                
                if (hasLightingIssue && hasEdgeIssue)
                {
                    result.Suggestions.Add("Move to better lighting and reposition document");
                }
                
                if (hasBlurIssue && hasEdgeIssue)
                {
                    result.Suggestions.Add("Stabilize camera and ensure document fits completely in frame");
                }

                return result;
            }
            catch (JSException ex)
            {
                // Handle JavaScript interop errors
                throw new InvalidOperationException($"Image quality analysis failed: {ex.Message}", ex);
            }
        }

        public async Task<BlurDetectionResult> DetectBlurAsync(string imageData)
        {
            try
            {
                // Call JavaScript blur detection function
                var result = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.detectBlur", imageData);
                
                var blurScore = result?.blurScore ?? 0.0;
                var threshold = result?.threshold ?? 0.5;
                
                return new BlurDetectionResult
                {
                    BlurScore = blurScore,
                    BlurThreshold = threshold,
                    IsBlurry = blurScore < threshold
                };
            }
            catch (JSException)
            {
                // Return default values if JavaScript call fails
                return new BlurDetectionResult
                {
                    BlurScore = 0.5,
                    BlurThreshold = 0.5,
                    IsBlurry = false
                };
            }
        }

        public async Task<LightingQualityResult> AssessLightingAsync(string imageData)
        {
            try
            {
                // Call JavaScript lighting assessment function
                var result = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.assessLighting", imageData);
                
                var lightingScore = result?.lightingScore ?? 0.0;
                var brightness = result?.brightness ?? 128.0;
                var contrast = result?.contrast ?? 0.5;
                
                return new LightingQualityResult
                {
                    LightingScore = lightingScore,
                    Brightness = brightness,
                    Contrast = contrast,
                    IsAdequate = lightingScore >= 0.6
                };
            }
            catch (JSException)
            {
                // Return default values if JavaScript call fails
                return new LightingQualityResult
                {
                    LightingScore = 0.6,
                    Brightness = 128.0,
                    Contrast = 0.5,
                    IsAdequate = true
                };
            }
        }

        public async Task<EdgeDetectionResult> DetectDocumentEdgesAsync(string imageData)
        {
            try
            {
                // Call JavaScript edge detection function
                var result = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.detectEdges", imageData);
                
                var edgeScore = result?.edgeScore ?? 0.0;
                var edgeCount = result?.edgeCount ?? 0;
                var confidence = result?.confidence ?? 0.0;
                
                return new EdgeDetectionResult
                {
                    EdgeScore = edgeScore,
                    EdgeCount = edgeCount,
                    Confidence = confidence,
                    HasClearEdges = edgeScore >= 0.7 && confidence >= 0.6
                };
            }
            catch (JSException)
            {
                // Return default values if JavaScript call fails
                return new EdgeDetectionResult
                {
                    EdgeScore = 0.5,
                    EdgeCount = 0,
                    Confidence = 0.5,
                    HasClearEdges = false
                };
            }
        }

        // Multi-Page Document Session Management
        public async Task<string> CreateDocumentSessionAsync()
        {
            ThrowIfDisposed();
            
            var sessionId = Guid.NewGuid().ToString();
            var session = new DocumentSession
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            };
            
            // Save to memory (existing functionality)
            _activeSessions[sessionId] = session;
            
            // Save to offline storage (new functionality)
            try
            {
                await _offlineStorage.SaveSessionAsync(session);
            }
            catch (OfflineStorageException ex)
            {
                // Log the error but continue - session is still in memory
                Console.WriteLine($"Warning: Failed to save session to offline storage: {ex.Message}");
            }
            
            // Queue operation if offline (new functionality)
            bool isOffline = false;
            try
            {
                isOffline = !await _connectivity.IsOnlineAsync();
            }
            catch (Exception ex)
            {
                // If connectivity check fails, assume offline mode
                Console.WriteLine($"Warning: Connectivity check failed, assuming offline mode: {ex.Message}");
                isOffline = true;
            }
            
            if (isOffline)
            {
                try
                {
                    var operation = new OfflineOperation
                    {
                        OperationType = "session_create",
                        Payload = JsonSerializer.Serialize(new { SessionId = sessionId, CreatedAt = session.CreatedAt }),
                        Priority = 1 // Normal priority for session creation
                    };
                    await _offlineQueue.QueueOperationAsync(operation);
                }
                catch (Exception ex)
                {
                    // Log the error but continue - session is still valid
                    Console.WriteLine($"Warning: Failed to queue offline operation: {ex.Message}");
                }
            }
            
            return sessionId;
        }

        public async Task AddPageToSessionAsync(string sessionId, CapturedImage capturedImage)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
            
            if (capturedImage == null)
                throw new ArgumentNullException(nameof(capturedImage));
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session '{sessionId}' not found");
            
            // Add to memory session (existing functionality)
            session.Pages.Add(capturedImage);
            session.CurrentPageIndex = session.Pages.Count - 1;
            session.UpdateActivity(); // Track activity for timeout management
            
            // Save image to offline storage (new functionality)
            try
            {
                await _offlineStorage.SaveImageAsync(capturedImage);
            }
            catch (OfflineStorageException ex)
            {
                // Log the error but continue - image is still in memory
                Console.WriteLine($"Warning: Failed to save image to offline storage: {ex.Message}");
            }
            
            // Update session in offline storage (new functionality)
            try
            {
                await _offlineStorage.SaveSessionAsync(session);
            }
            catch (OfflineStorageException ex)
            {
                // Log the error but continue - session is still in memory
                Console.WriteLine($"Warning: Failed to save session to offline storage: {ex.Message}");
            }
            
            // Queue operation if offline (new functionality)
            bool isOffline = false;
            try
            {
                isOffline = !await _connectivity.IsOnlineAsync();
            }
            catch (Exception ex)
            {
                // If connectivity check fails, assume offline mode
                Console.WriteLine($"Warning: Connectivity check failed, assuming offline mode: {ex.Message}");
                isOffline = true;
            }
            
            if (isOffline)
            {
                try
                {
                    var operation = new OfflineOperation
                    {
                        OperationType = "page_add",
                        Payload = JsonSerializer.Serialize(new { 
                            SessionId = sessionId, 
                            ImageId = capturedImage.Timestamp.ToString("yyyyMMddHHmmssfff"),
                            PageIndex = session.CurrentPageIndex 
                        }),
                        Priority = 0 // High priority for captured images
                    };
                    await _offlineQueue.QueueOperationAsync(operation);
                }
                catch (Exception ex)
                {
                    // Log the error but continue - page is still added to session
                    Console.WriteLine($"Warning: Failed to queue offline operation: {ex.Message}");
                }
            }
        }

        public async Task<CapturedImage[]> GetSessionPagesAsync(string sessionId)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session '{sessionId}' not found");
                
            session.UpdateActivity(); // Track activity for timeout management
            return session.Pages.ToArray();
        }

        public async Task<DocumentSession> GetDocumentSessionAsync(string sessionId)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session '{sessionId}' not found");
                
            session.UpdateActivity(); // Track activity for timeout management
            return session;
        }

        public async Task RemovePageFromSessionAsync(string sessionId, int pageIndex)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session '{sessionId}' not found");
                
            if (pageIndex < 0 || pageIndex >= session.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page index {pageIndex} is out of range");
                
            session.Pages.RemoveAt(pageIndex);
            
            // Adjust current page index if necessary
            if (session.CurrentPageIndex >= pageIndex && session.CurrentPageIndex > 0)
            {
                session.CurrentPageIndex--;
            }
            else if (session.CurrentPageIndex >= session.Pages.Count)
            {
                session.CurrentPageIndex = Math.Max(0, session.Pages.Count - 1);
            }
            
            session.UpdateActivity(); // Track activity for timeout management
            
            // Queue operation if offline (new functionality)
            bool isOffline = false;
            try
            {
                isOffline = !await _connectivity.IsOnlineAsync();
            }
            catch (Exception ex)
            {
                // If connectivity check fails, assume offline mode
                Console.WriteLine($"Warning: Connectivity check failed, assuming offline mode: {ex.Message}");
                isOffline = true;
            }
            
            if (isOffline)
            {
                try
                {
                    var operation = new OfflineOperation
                    {
                        OperationType = "page_remove",
                        Payload = JsonSerializer.Serialize(new { 
                            SessionId = sessionId, 
                            PageIndex = pageIndex 
                        }),
                        Priority = 2 // Lower priority for remove operations
                    };
                    await _offlineQueue.QueueOperationAsync(operation);
                }
                catch (Exception ex)
                {
                    // Log the error but continue - page is still removed from session
                    Console.WriteLine($"Warning: Failed to queue offline operation: {ex.Message}");
                }
            }
        }

        public async Task ReorderPagesInSessionAsync(string sessionId, int fromIndex, int toIndex)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session '{sessionId}' not found");
                
            if (fromIndex < 0 || fromIndex >= session.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
                
            if (toIndex < 0 || toIndex >= session.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(toIndex));
                
            if (fromIndex == toIndex)
                return;
                
            var page = session.Pages[fromIndex];
            session.Pages.RemoveAt(fromIndex);
            session.Pages.Insert(toIndex, page);
            
            // Update current page index if it was affected by the reorder
            if (session.CurrentPageIndex == fromIndex)
            {
                session.CurrentPageIndex = toIndex;
            }
            else if (fromIndex < session.CurrentPageIndex && toIndex >= session.CurrentPageIndex)
            {
                session.CurrentPageIndex--;
            }
            else if (fromIndex > session.CurrentPageIndex && toIndex <= session.CurrentPageIndex)
            {
                session.CurrentPageIndex++;
            }
            
            session.UpdateActivity(); // Track activity for timeout management
        }

        public async Task ClearDocumentSessionAsync(string sessionId)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException($"Session '{sessionId}' not found");
                
            session.Pages.Clear();
            session.CurrentPageIndex = 0;
            session.UpdateActivity(); // Track activity for timeout management
        }

        // Session Cleanup and Disposal Implementation
        public async Task DisposeDocumentSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                // Queue disposal operation if offline (new functionality)
                bool isOffline = false;
                try
                {
                    isOffline = !await _connectivity.IsOnlineAsync();
                }
                catch (Exception ex)
                {
                    // If connectivity check fails, assume offline mode
                    Console.WriteLine($"Warning: Connectivity check failed, assuming offline mode: {ex.Message}");
                    isOffline = true;
                }
                
                if (isOffline)
                {
                    try
                    {
                        var operation = new OfflineOperation
                        {
                            OperationType = "session_dispose",
                            Payload = JsonSerializer.Serialize(new { SessionId = sessionId, DisposedAt = DateTime.UtcNow }),
                            Priority = 2 // Low priority for disposal operations
                        };
                        await _offlineQueue.QueueOperationAsync(operation);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue - session disposal should proceed
                        Console.WriteLine($"Warning: Failed to queue offline operation: {ex.Message}");
                    }
                }
                
                // Clear all pages to release memory
                session.Pages.Clear();
                
                // Remove session from active sessions
                _activeSessions.Remove(sessionId);
                
                // Note: We don't remove from offline storage immediately to allow for sync
                // The sync service will handle cleanup after successful upload
            }
        }

        public async Task CleanupInactiveSessionsAsync()
        {
            var expiredSessionIds = _activeSessions
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessionIds)
            {
                await DisposeDocumentSessionAsync(sessionId);
            }
        }

        public async Task<bool> IsSessionActiveAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return false;

            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            // Check if session has expired
            if (session.IsExpired)
            {
                // Clean up expired session
                await DisposeDocumentSessionAsync(sessionId);
                return false;
            }

            return true;
        }

        // IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up managed resources
                    try
                    {
                        // Stop any active camera stream
                        if (_currentStream != null)
                        {
                            // Note: Cannot await in Dispose, but JS calls should be fire-and-forget for cleanup
                            _jsRuntime.InvokeVoidAsync("camera.stopStream", _currentStream.StreamId);
                            _currentStream = null;
                        }

                        // Dispose all active document sessions
                        foreach (var session in _activeSessions.Values)
                        {
                            session.Pages.Clear();
                        }
                        _activeSessions.Clear();
                    }
                    catch
                    {
                        // Ignore errors during disposal - best effort cleanup
                    }
                }

                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CameraService));
            }
        }
    }
}