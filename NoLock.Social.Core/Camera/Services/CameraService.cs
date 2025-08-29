using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoLock.Social.Core.Camera.Interfaces;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Common.Guards;
using NoLock.Social.Core.Resources;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Common.Results;

namespace NoLock.Social.Core.Camera.Services
{
    public class CameraService : ICameraService, IDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<CameraService> _logger;
        private CameraPermissionState _currentPermissionState = CameraPermissionState.Prompt;
        private CameraStream _currentStream;
        private CameraControlSettings _controlSettings = new CameraControlSettings();
        private readonly Dictionary<string, DocumentSession> _activeSessions = new();
        private bool _disposed;

        public CameraService(
            IJSRuntime jsRuntime,
            ILogger<CameraService> logger = null)
        {
            _jsRuntime = Guard.AgainstNull(jsRuntime);
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            ThrowIfDisposed();
            
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(
                async () =>
                {
                    // Initialize service - currently no persistent storage
                    // Sessions are kept in memory only
                    _logger?.LogInformation("Camera service initialized");
                },
                "InitializeAsync"
            );
            
            // Result is always successful due to graceful error handling, but we maintain the pattern
            // for consistency and potential future use of the result information
        }

        public async Task<CameraPermissionState> GetPermissionStateAsync()
        {
            var result = await _jsRuntime.InvokeAsync<string>("cameraPermissions.getState");
            _currentPermissionState = ParsePermissionStateResult(result, CameraPermissionState.NotRequested);
            return _currentPermissionState;
        }

        public async Task<CameraPermissionState> RequestPermission()
        {
            var result = await _jsRuntime.InvokeAsync<string>("cameraPermissions.request");
            _currentPermissionState = ParsePermissionStateResult(result, CameraPermissionState.Denied);
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

            // Use ExecuteWithLogging for error handling
            var result = await (_logger ?? NullLogger<CameraService>.Instance)
                .ExecuteWithLogging(async () =>
                {
                    // Call JavaScript to stop the camera stream
                    await _jsRuntime.InvokeVoidAsync("camera.stopStream", _currentStream.StreamId);
                },
                "StopStreamAsync");
            
            // Clear the current stream reference regardless of result
            // Stream might already be stopped, so we clear it anyway
            _currentStream = null;
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
            
            // Image captured successfully - no persistent storage currently
            
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
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Check if torch is supported before attempting to toggle
                if (!await IsTorchSupportedAsync())
                {
                    return false;
                }

                // Call JavaScript to toggle torch/flash
                var jsResult = await _jsRuntime.InvokeAsync<bool>("camera.setTorch", enabled);
                
                // Update settings state if successful
                if (jsResult)
                {
                    _controlSettings.IsTorchEnabled = enabled;
                }
                
                return jsResult;
            },
            "ToggleTorchAsync");
            
            // Return false on failure, or the actual result on success
            return result.IsSuccess ? result.Value : false;
        }

        public async Task<bool> IsTorchSupportedAsync()
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript to check torch support
                var supported = await _jsRuntime.InvokeAsync<bool>("camera.getTorchSupport");
                
                // Update settings state
                _controlSettings.HasTorchSupport = supported;
                
                return supported;
            },
            "IsTorchSupportedAsync");
            
            // If JavaScript call fails, assume no torch support
            if (!result.IsSuccess)
            {
                _controlSettings.HasTorchSupport = false;
                return false;
            }
            
            return result.Value;
        }

        // Placeholder implementations for remaining interface methods
        public async Task<bool> SwitchCameraAsync(string deviceId)
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Stop current stream before switching
                await StopStreamAsync();
                
                // Call JavaScript to switch to specified camera device
                var switchResult = await _jsRuntime.InvokeAsync<bool>("camera.switchCamera", deviceId);
                
                // If switch was successful, update control settings with new camera ID
                if (switchResult)
                {
                    _controlSettings.CurrentCameraId = deviceId;
                    
                    // Start new stream with switched camera
                    await StartStreamAsync();
                }
                
                return switchResult;
            },
            "SwitchCameraAsync");
            
            // Camera switch failed, return false
            return result.IsSuccess ? result.Value : false;
        }

        public async Task<bool> SetZoomAsync(double zoomLevel)
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                if (!await IsZoomLevelValidAsync(zoomLevel))
                    return false;

                var setResult = await _jsRuntime.InvokeAsync<bool>("camera.setZoom", zoomLevel);
                
                if (setResult)
                {
                    _controlSettings.ZoomLevel = zoomLevel;
                }
                
                return setResult;
            },
            "SetZoomAsync");
            
            return result.IsSuccess ? result.Value : false;
        }

        public async Task<double> GetZoomAsync()
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript to get current zoom level
                var zoomLevel = await _jsRuntime.InvokeAsync<double>("camera.getZoom");
                
                // Update settings state
                _controlSettings.ZoomLevel = zoomLevel;
                
                return zoomLevel;
            },
            "GetZoomAsync");
            
            // If getting zoom fails, return default zoom of 1.0
            return result.IsSuccess ? result.Value : 1.0;
        }

        public async Task<string[]> GetAvailableCamerasAsync()
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript to enumerate available camera devices
                var devices = await _jsRuntime.InvokeAsync<string[]>("camera.getAvailableCameras");
                
                return devices ?? new string[0];
            },
            "GetAvailableCamerasAsync");
            
            // If enumeration fails, return empty array
            return result.IsSuccess ? result.Value : new string[0];
        }

        public async Task<bool> IsZoomSupportedAsync()
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript to check zoom support
                var supported = await _jsRuntime.InvokeAsync<bool>("camera.getZoomSupport");
                
                // Update settings state
                _controlSettings.HasZoomSupport = supported;
                
                return supported;
            },
            "IsZoomSupportedAsync");
            
            // If JavaScript call fails, assume no zoom support
            if (!result.IsSuccess)
            {
                _controlSettings.HasZoomSupport = false;
                return false;
            }
            
            return result.Value;
        }

        public async Task<double> GetMaxZoomAsync()
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript to get maximum zoom level
                var maxZoom = await _jsRuntime.InvokeAsync<double>("camera.getMaxZoom");
                
                // Update settings state
                _controlSettings.MaxZoomLevel = maxZoom;
                
                return maxZoom;
            },
            "GetMaxZoomAsync");
            
            // If getting max zoom fails, return default max of 3.0
            return result.IsSuccess ? result.Value : 3.0;
        }

        public async Task<ImageQualityResult> ValidateImageQualityAsync(CapturedImage capturedImage)
        {
            ValidateImageInput(capturedImage);

            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                var qualityAnalysis = await PerformQualityAnalysisAsync(capturedImage.ImageData);
                var qualityResult = CreateQualityResult(qualityAnalysis);
                
                AddQualityIssuesAndSuggestions(qualityResult, qualityAnalysis);
                
                return qualityResult;
            },
            "ValidateImageQualityAsync");

            if (result.IsFailure)
            {
                HandleQualityAnalysisError(result);
            }

            return result.Value;
        }

        public async Task<BlurDetectionResult> DetectBlurAsync(string imageData)
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript blur detection function
                var jsResult = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.detectBlur", imageData);
                
                var blurScore = jsResult?.blurScore ?? 0.0;
                var threshold = jsResult?.threshold ?? 0.5;
                
                return new BlurDetectionResult
                {
                    BlurScore = blurScore,
                    BlurThreshold = threshold,
                    IsBlurry = blurScore < threshold
                };
            },
            "DetectBlurAsync");

            // Return default values if JavaScript call fails
            if (result.IsFailure)
            {
                return new BlurDetectionResult
                {
                    BlurScore = 0.5,
                    BlurThreshold = 0.5,
                    IsBlurry = false
                };
            }

            return result.Value;
        }

        public async Task<LightingQualityResult> AssessLightingAsync(string imageData)
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript lighting assessment function
                var jsResult = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.assessLighting", imageData);
                
                var lightingScore = jsResult?.lightingScore ?? 0.0;
                var brightness = jsResult?.brightness ?? 128.0;
                var contrast = jsResult?.contrast ?? 0.5;
                
                return new LightingQualityResult
                {
                    LightingScore = lightingScore,
                    Brightness = brightness,
                    Contrast = contrast,
                    IsAdequate = lightingScore >= 0.6
                };
            },
            "AssessLightingAsync");

            // Return default values if JavaScript call fails
            if (result.IsFailure)
            {
                return new LightingQualityResult
                {
                    LightingScore = 0.6,
                    Brightness = 128.0,
                    Contrast = 0.5,
                    IsAdequate = true
                };
            }

            return result.Value;
        }

        public async Task<EdgeDetectionResult> DetectDocumentEdgesAsync(string imageData)
        {
            var result = await (_logger ?? NullLogger<CameraService>.Instance).ExecuteWithLogging(async () =>
            {
                // Call JavaScript edge detection function
                var jsResult = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.detectEdges", imageData);
                
                var edgeScore = jsResult?.edgeScore ?? 0.0;
                var edgeCount = jsResult?.edgeCount ?? 0;
                var confidence = jsResult?.confidence ?? 0.0;
                
                return new EdgeDetectionResult
                {
                    EdgeScore = edgeScore,
                    EdgeCount = edgeCount,
                    Confidence = confidence,
                    HasClearEdges = edgeScore >= 0.7 && confidence >= 0.6
                };
            },
            "DetectDocumentEdgesAsync");

            // Return default values if JavaScript call fails
            if (result.IsFailure)
            {
                return new EdgeDetectionResult
                {
                    EdgeScore = 0.5,
                    EdgeCount = 0,
                    Confidence = 0.5,
                    HasClearEdges = false
                };
            }

            return result.Value;
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
            
            // Session created in memory only - no persistent storage
            
            return sessionId;
        }

        public async Task AddPageToSessionAsync(string sessionId, CapturedImage capturedImage)
        {
            ThrowIfDisposed();
            var session = ValidateAndGetSession(sessionId);
            
            if (capturedImage == null)
                throw new ArgumentNullException(nameof(capturedImage));
                
            // Add to memory session (existing functionality)
            session.Pages.Add(capturedImage);
            session.CurrentPageIndex = session.Pages.Count - 1;
            session.UpdateActivity(); // Track activity for timeout management
            
            // Page added to session in memory only - no persistent storage
        }

        public async Task<CapturedImage[]> GetSessionPagesAsync(string sessionId)
        {
            ThrowIfDisposed();
            
            Guard.AgainstNullOrEmpty(sessionId, ValidationMessages.SessionIdRequired);
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException(string.Format(ValidationMessages.SessionNotFound, sessionId));
                
            session.UpdateActivity(); // Track activity for timeout management
            return session.Pages.ToArray();
        }

        public async Task<DocumentSession> GetDocumentSessionAsync(string sessionId)
        {
            ThrowIfDisposed();
            
            Guard.AgainstNullOrEmpty(sessionId, ValidationMessages.SessionIdRequired);
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException(string.Format(ValidationMessages.SessionNotFound, sessionId));
                
            session.UpdateActivity(); // Track activity for timeout management
            return session;
        }

        public async Task RemovePageFromSessionAsync(string sessionId, int pageIndex)
        {
            ThrowIfDisposed();
            
            Guard.AgainstNullOrEmpty(sessionId, ValidationMessages.SessionIdRequired);
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException(string.Format(ValidationMessages.SessionNotFound, sessionId));
                
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
            
            // Page removed from session in memory only - no persistent storage
        }

        public async Task ReorderPagesInSessionAsync(string sessionId, int fromIndex, int toIndex)
        {
            ThrowIfDisposed();
            var session = ValidateAndGetSession(sessionId);
            ValidateReorderIndices(fromIndex, toIndex, session.Pages.Count);
            
            if (fromIndex == toIndex) return;
            
            ReorderPages(session, fromIndex, toIndex);
            session.UpdateActivity();
        }

        public async Task ClearDocumentSessionAsync(string sessionId)
        {
            ThrowIfDisposed();
            
            Guard.AgainstNullOrEmpty(sessionId, ValidationMessages.SessionIdRequired);
                
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException(string.Format(ValidationMessages.SessionNotFound, sessionId));
                
            session.Pages.Clear();
            session.CurrentPageIndex = 0;
            session.UpdateActivity(); // Track activity for timeout management
        }

        // Session Cleanup and Disposal Implementation
        public async Task DisposeDocumentSessionAsync(string sessionId)
        {
            Guard.AgainstNullOrEmpty(sessionId, ValidationMessages.SessionIdRequired);

            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
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

        // Helper methods for permission parsing
        private static CameraPermissionState ParsePermissionStateResult(string result, CameraPermissionState defaultState)
        {
            return result?.ToLowerInvariant() switch
            {
                "granted" => CameraPermissionState.Granted,
                "denied" => CameraPermissionState.Denied,
                "prompt" => CameraPermissionState.Prompt,
                "not-requested" => CameraPermissionState.NotRequested,
                _ => defaultState
            };
        }

        // Helper methods for camera controls
        private async Task<bool> IsZoomLevelValidAsync(double zoomLevel)
        {
            if (zoomLevel < 1.0) return false;
            
            var maxZoom = await GetMaxZoomAsync();
            return zoomLevel <= maxZoom;
        }

        // Helper methods for session management
        private DocumentSession ValidateAndGetSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException(ValidationMessages.SessionIdRequired, nameof(sessionId));
            
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                throw new InvalidOperationException(string.Format(ValidationMessages.SessionNotFound, sessionId));
                
            return session;
        }

        private static void ValidateReorderIndices(int fromIndex, int toIndex, int pageCount)
        {
            if (fromIndex < 0 || fromIndex >= pageCount)
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
                
            if (toIndex < 0 || toIndex >= pageCount)
                throw new ArgumentOutOfRangeException(nameof(toIndex));
        }

        private static void ReorderPages(DocumentSession session, int fromIndex, int toIndex)
        {
            var page = session.Pages[fromIndex];
            session.Pages.RemoveAt(fromIndex);
            session.Pages.Insert(toIndex, page);
            
            session.CurrentPageIndex = CalculateNewCurrentPageIndex(
                session.CurrentPageIndex, fromIndex, toIndex);
        }

        private static int CalculateNewCurrentPageIndex(int currentIndex, int fromIndex, int toIndex)
        {
            if (currentIndex == fromIndex)
                return toIndex;
            
            if (fromIndex < currentIndex && toIndex >= currentIndex)
                return currentIndex - 1;
            
            if (fromIndex > currentIndex && toIndex <= currentIndex)
                return currentIndex + 1;
            
            return currentIndex;
        }

        // Helper methods for ValidateImageQualityAsync
        private static void ValidateImageInput(CapturedImage capturedImage)
        {
            if (capturedImage == null || string.IsNullOrEmpty(capturedImage?.ImageData))
            {
                throw new ArgumentException("Invalid captured image data", nameof(capturedImage));
            }
        }

        private async Task<QualityAnalysisData> PerformQualityAnalysisAsync(string imageData)
        {
            // Call the methods directly without the defensive error handling - let exceptions bubble up
            var blurResult = await DetectBlurDirectAsync(imageData);
            var lightingResult = await AssessLightingDirectAsync(imageData);
            var edgeResult = await DetectDocumentEdgesDirectAsync(imageData);

            return new QualityAnalysisData(blurResult, lightingResult, edgeResult);
        }

        private async Task<BlurDetectionResult> DetectBlurDirectAsync(string imageData)
        {
            var jsResult = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.detectBlur", imageData);
            
            var blurScore = jsResult?.blurScore ?? 0.0;
            var threshold = jsResult?.threshold ?? 0.5;
            
            return new BlurDetectionResult
            {
                BlurScore = blurScore,
                BlurThreshold = threshold,
                IsBlurry = blurScore < threshold
            };
        }

        private async Task<LightingQualityResult> AssessLightingDirectAsync(string imageData)
        {
            var jsResult = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.assessLighting", imageData);
            
            var lightingScore = jsResult?.lightingScore ?? 0.0;
            var brightness = jsResult?.brightness ?? 128.0;
            var contrast = jsResult?.contrast ?? 0.5;
            
            return new LightingQualityResult
            {
                LightingScore = lightingScore,
                Brightness = brightness,
                Contrast = contrast,
                IsAdequate = lightingScore >= 0.6
            };
        }

        private async Task<EdgeDetectionResult> DetectDocumentEdgesDirectAsync(string imageData)
        {
            var jsResult = await _jsRuntime.InvokeAsync<dynamic>("imageQuality.detectEdges", imageData);
            
            var edgeScore = jsResult?.edgeScore ?? 0.0;
            var edgeCount = jsResult?.edgeCount ?? 0;
            var confidence = jsResult?.confidence ?? 0.0;
            
            return new EdgeDetectionResult
            {
                EdgeScore = edgeScore,
                EdgeCount = edgeCount,
                Confidence = confidence,
                HasClearEdges = edgeScore >= 0.7 && confidence >= 0.6
            };
        }

        private static ImageQualityResult CreateQualityResult(QualityAnalysisData analysis)
        {
            var overallScore = CalculateOverallScore(analysis);
            
            return new ImageQualityResult
            {
                OverallScore = overallScore,
                BlurScore = analysis.BlurResult.BlurScore,
                LightingScore = analysis.LightingResult.LightingScore,
                EdgeDetectionScore = analysis.EdgeResult.EdgeScore
            };
        }

        private static int CalculateOverallScore(QualityAnalysisData analysis)
        {
            return (int)((analysis.BlurResult.BlurScore * 0.4 +
                         analysis.LightingResult.LightingScore * 0.3 +
                         analysis.EdgeResult.EdgeScore * 0.3) * 100);
        }

        private static void AddQualityIssuesAndSuggestions(ImageQualityResult result, QualityAnalysisData analysis)
        {
            var issues = new QualityIssues(analysis);
            
            AddBlurIssuesAndSuggestions(result, analysis.BlurResult, issues.HasBlurIssue);
            AddLightingIssuesAndSuggestions(result, analysis.LightingResult, issues.HasLightingIssue);
            AddEdgeIssuesAndSuggestions(result, analysis.EdgeResult, issues.HasEdgeIssue);
            AddCombinationSuggestions(result, issues);
        }

        private static void AddBlurIssuesAndSuggestions(ImageQualityResult result, BlurDetectionResult blurResult, bool hasBlurIssue)
        {
            if (!hasBlurIssue) return;
            
            result.Issues.Add("Image appears blurry");
            
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

        private static void AddLightingIssuesAndSuggestions(ImageQualityResult result, LightingQualityResult lightingResult, bool hasLightingIssue)
        {
            if (!hasLightingIssue) return;
            
            result.Issues.Add("Poor lighting conditions");
            
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

        private static void AddEdgeIssuesAndSuggestions(ImageQualityResult result, EdgeDetectionResult edgeResult, bool hasEdgeIssue)
        {
            if (!hasEdgeIssue) return;
            
            result.Issues.Add("Document edges not clearly detected");
            
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

        private static void AddCombinationSuggestions(ImageQualityResult result, QualityIssues issues)
        {
            if (issues.HasBlurIssue && issues.HasLightingIssue)
            {
                result.Suggestions.Add("Use tripod or stable surface in well-lit area");
            }
            
            if (issues.HasLightingIssue && issues.HasEdgeIssue)
            {
                result.Suggestions.Add("Move to better lighting and reposition document");
            }
            
            if (issues.HasBlurIssue && issues.HasEdgeIssue)
            {
                result.Suggestions.Add("Stabilize camera and ensure document fits completely in frame");
            }
        }

        private static void HandleQualityAnalysisError(Result<ImageQualityResult> result)
        {
            if (result.Exception is JSException jsEx)
            {
                throw new InvalidOperationException($"Image quality analysis failed: {jsEx.Message}", jsEx);
            }
            throw result.Exception;
        }

        // Helper classes for quality analysis
        private readonly record struct QualityAnalysisData(
            BlurDetectionResult BlurResult,
            LightingQualityResult LightingResult,
            EdgeDetectionResult EdgeResult);

        private readonly record struct QualityIssues(
            bool HasBlurIssue,
            bool HasLightingIssue,
            bool HasEdgeIssue)
        {
            public QualityIssues(QualityAnalysisData analysis) : this(
                analysis.BlurResult.IsBlurry,
                !analysis.LightingResult.IsAdequate,
                !analysis.EdgeResult.HasClearEdges)
            {
            }
        }
    }
}