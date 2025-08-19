using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.ImageProcessing.Interfaces;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Provides wake lock management to prevent device sleep during OCR processing.
    /// Uses the Web Wake Lock API through JavaScript interop for browser integration.
    /// Includes visibility tracking and user preference management.
    /// </summary>
    public class WakeLockService : IWakeLockService, IDisposable
    {
        private readonly IJSRuntimeWrapper _jsRuntime;
        private readonly ILogger<WakeLockService> _logger;
        
        private bool _isWakeLockActive;
        private bool _isPageVisible = true;
        private bool _isVisibilityMonitoring;
        private bool _disposed;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Event raised when the page visibility state changes.
        /// </summary>
        public event EventHandler<VisibilityChangedEventArgs>? VisibilityChanged;

        /// <summary>
        /// Event raised when the wake lock status changes.
        /// </summary>
        public event EventHandler<WakeLockStatusChangedEventArgs>? WakeLockStatusChanged;

        /// <summary>
        /// Gets a value indicating whether wake lock is currently active.
        /// </summary>
        public bool IsWakeLockActive 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    return _isWakeLockActive; 
                } 
            } 
        }

        /// <summary>
        /// Gets a value indicating whether the page is currently visible to the user.
        /// </summary>
        public bool IsPageVisible 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    return _isPageVisible; 
                } 
            } 
        }

        /// <summary>
        /// Initializes a new instance of the WakeLockService class.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for interop operations.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public WakeLockService(IJSRuntimeWrapper jsRuntime, ILogger<WakeLockService> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Attempts to acquire a screen wake lock to prevent the device from sleeping.
        /// </summary>
        /// <param name="reason">Optional reason for acquiring the wake lock.</param>
        /// <returns>A task that represents the asynchronous operation with wake lock result.</returns>
        public async Task<WakeLockResult> AcquireWakeLockAsync(string reason = "OCR Processing")
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WakeLockService));
            }

            var previousState = IsWakeLockActive;
            
            try
            {
                _logger.LogInformation("Attempting to acquire wake lock. Reason: {Reason}", reason);

                // Check if wake lock is already active
                if (IsWakeLockActive)
                {
                    _logger.LogDebug("Wake lock is already active");
                    return WakeLockResult.Success(WakeLockOperationType.Acquire);
                }

                // Check browser support first
                var isSupportedResult = await CheckBrowserSupportAsync();
                if (!isSupportedResult.IsSuccess)
                {
                    _logger.LogWarning("Wake Lock API is not supported in this browser");
                    return isSupportedResult;
                }

                // Attempt to acquire wake lock through JavaScript
                var jsResult = await _jsRuntime.InvokeAsync<bool>("wakeLockInterop.acquireWakeLock");
                
                if (jsResult)
                {
                    lock (_lockObject)
                    {
                        _isWakeLockActive = true;
                    }

                    _logger.LogInformation("Wake lock acquired successfully. Reason: {Reason}", reason);
                    
                    var result = WakeLockResult.Success(WakeLockOperationType.Acquire);
                    RaiseWakeLockStatusChanged(true, previousState, WakeLockOperationType.Acquire, result, reason);
                    
                    return result;
                }
                else
                {
                    var errorMessage = "Failed to acquire wake lock through JavaScript interop";
                    _logger.LogWarning(errorMessage);
                    
                    var result = WakeLockResult.Failure(
                        WakeLockOperationType.Acquire,
                        errorMessage,
                        "JavaScript interop returned false");
                    
                    RaiseWakeLockStatusChanged(false, previousState, WakeLockOperationType.Acquire, result, reason);
                    return result;
                }
            }
            catch (JSException jsEx)
            {
                var errorMessage = $"JavaScript error while acquiring wake lock: {jsEx.Message}";
                _logger.LogError(jsEx, errorMessage);
                
                var result = WakeLockResult.Failure(
                    WakeLockOperationType.Acquire,
                    errorMessage,
                    jsEx.ToString());
                
                RaiseWakeLockStatusChanged(false, previousState, WakeLockOperationType.Acquire, result, reason);
                return result;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unexpected error while acquiring wake lock: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                
                var result = WakeLockResult.Failure(
                    WakeLockOperationType.Acquire,
                    errorMessage,
                    ex.ToString());
                
                RaiseWakeLockStatusChanged(false, previousState, WakeLockOperationType.Acquire, result, reason);
                return result;
            }
        }

        /// <summary>
        /// Releases the currently active screen wake lock.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation with wake lock result.</returns>
        public async Task<WakeLockResult> ReleaseWakeLockAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WakeLockService));
            }

            var previousState = IsWakeLockActive;

            try
            {
                _logger.LogInformation("Attempting to release wake lock");

                // Check if wake lock is already inactive
                if (!IsWakeLockActive)
                {
                    _logger.LogDebug("Wake lock is already inactive");
                    return WakeLockResult.Success(WakeLockOperationType.Release);
                }

                // Attempt to release wake lock through JavaScript
                var jsResult = await _jsRuntime.InvokeAsync<bool>("wakeLockInterop.releaseWakeLock");
                
                if (jsResult)
                {
                    lock (_lockObject)
                    {
                        _isWakeLockActive = false;
                    }

                    _logger.LogInformation("Wake lock released successfully");
                    
                    var result = WakeLockResult.Success(WakeLockOperationType.Release);
                    RaiseWakeLockStatusChanged(false, previousState, WakeLockOperationType.Release, result);
                    
                    return result;
                }
                else
                {
                    var errorMessage = "Failed to release wake lock through JavaScript interop";
                    _logger.LogWarning(errorMessage);
                    
                    var result = WakeLockResult.Failure(
                        WakeLockOperationType.Release,
                        errorMessage,
                        "JavaScript interop returned false");
                    
                    RaiseWakeLockStatusChanged(_isWakeLockActive, previousState, WakeLockOperationType.Release, result);
                    return result;
                }
            }
            catch (JSException jsEx)
            {
                var errorMessage = $"JavaScript error while releasing wake lock: {jsEx.Message}";
                _logger.LogError(jsEx, errorMessage);
                
                var result = WakeLockResult.Failure(
                    WakeLockOperationType.Release,
                    errorMessage,
                    jsEx.ToString());
                
                RaiseWakeLockStatusChanged(_isWakeLockActive, previousState, WakeLockOperationType.Release, result);
                return result;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unexpected error while releasing wake lock: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                
                var result = WakeLockResult.Failure(
                    WakeLockOperationType.Release,
                    errorMessage,
                    ex.ToString());
                
                RaiseWakeLockStatusChanged(_isWakeLockActive, previousState, WakeLockOperationType.Release, result);
                return result;
            }
        }

        /// <summary>
        /// Starts monitoring page visibility changes.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task StartVisibilityMonitoringAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WakeLockService));
            }

            if (_isVisibilityMonitoring)
            {
                _logger.LogDebug("Visibility monitoring is already active");
                return;
            }

            try
            {
                _logger.LogInformation("Starting visibility monitoring");

                // Create a .NET object reference for JavaScript callbacks
                var objRef = DotNetObjectReference.Create(this);
                await _jsRuntime.InvokeVoidAsync("wakeLockInterop.startVisibilityMonitoring", objRef);
                
                _isVisibilityMonitoring = true;
                _logger.LogInformation("Visibility monitoring started successfully");
            }
            catch (JSException jsEx)
            {
                var errorMessage = $"JavaScript error while starting visibility monitoring: {jsEx.Message}";
                _logger.LogError(jsEx, errorMessage);
                throw new InvalidOperationException(errorMessage, jsEx);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unexpected error while starting visibility monitoring: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                throw;
            }
        }

        /// <summary>
        /// Stops monitoring page visibility changes.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task StopVisibilityMonitoringAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WakeLockService));
            }

            if (!_isVisibilityMonitoring)
            {
                _logger.LogDebug("Visibility monitoring is already inactive");
                return;
            }

            try
            {
                _logger.LogInformation("Stopping visibility monitoring");

                await _jsRuntime.InvokeVoidAsync("wakeLockInterop.stopVisibilityMonitoring");
                
                _isVisibilityMonitoring = false;
                _logger.LogInformation("Visibility monitoring stopped successfully");
            }
            catch (JSException jsEx)
            {
                var errorMessage = $"JavaScript error while stopping visibility monitoring: {jsEx.Message}";
                _logger.LogError(jsEx, errorMessage);
                throw new InvalidOperationException(errorMessage, jsEx);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unexpected error while stopping visibility monitoring: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                throw;
            }
        }

        /// <summary>
        /// JavaScript callback method for visibility change events.
        /// This method is called from JavaScript when the page visibility changes.
        /// </summary>
        /// <param name="isVisible">Whether the page is currently visible.</param>
        /// <param name="visibilityState">The browser's visibility state string.</param>
        [JSInvokable]
        public void OnVisibilityChanged(bool isVisible, string? visibilityState)
        {
            var previousState = IsPageVisible;
            
            lock (_lockObject)
            {
                _isPageVisible = isVisible;
            }

            _logger.LogDebug("Page visibility changed: {IsVisible} (state: {VisibilityState})", isVisible, visibilityState);

            var eventArgs = new VisibilityChangedEventArgs(isVisible, previousState, visibilityState);
            VisibilityChanged?.Invoke(this, eventArgs);

            // Auto-release wake lock when page becomes hidden (optional behavior)
            if (!isVisible && IsWakeLockActive)
            {
                _logger.LogInformation("Page became hidden, automatically releasing wake lock");
                _ = Task.Run(async () => await ReleaseWakeLockAsync());
            }
        }

        /// <summary>
        /// Checks if the browser supports the Wake Lock API.
        /// </summary>
        /// <returns>A wake lock result indicating browser support status.</returns>
        private async Task<WakeLockResult> CheckBrowserSupportAsync()
        {
            try
            {
                var isSupported = await _jsRuntime.InvokeAsync<bool>("wakeLockInterop.isWakeLockSupported");
                
                if (!isSupported)
                {
                    return WakeLockResult.Failure(
                        WakeLockOperationType.Acquire,
                        "Wake Lock API is not supported in this browser",
                        "The browser does not support the Screen Wake Lock API",
                        false);
                }

                return WakeLockResult.Success(WakeLockOperationType.Acquire);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Wake Lock API support");
                return WakeLockResult.Failure(
                    WakeLockOperationType.Acquire,
                    "Unable to determine Wake Lock API support",
                    ex.Message,
                    false);
            }
        }

        /// <summary>
        /// Raises the WakeLockStatusChanged event.
        /// </summary>
        private void RaiseWakeLockStatusChanged(
            bool isActive, 
            bool previousState, 
            WakeLockOperationType operationType, 
            WakeLockResult result, 
            string? reason = null)
        {
            var eventArgs = new WakeLockStatusChangedEventArgs(
                isActive, 
                previousState, 
                operationType, 
                result, 
                reason);

            WakeLockStatusChanged?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Releases all resources used by the WakeLockService.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                // Release wake lock if active
                if (IsWakeLockActive)
                {
                    _logger.LogInformation("Disposing WakeLockService, releasing active wake lock");
                    _ = Task.Run(async () => await ReleaseWakeLockAsync());
                }

                // Stop visibility monitoring if active
                if (_isVisibilityMonitoring)
                {
                    _logger.LogInformation("Disposing WakeLockService, stopping visibility monitoring");
                    _ = Task.Run(async () => await StopVisibilityMonitoringAsync());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during WakeLockService disposal");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}