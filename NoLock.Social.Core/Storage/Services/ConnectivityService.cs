using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Storage.Interfaces;

namespace NoLock.Social.Core.Storage.Services
{
    /// <summary>
    /// Service for monitoring network connectivity status and changes
    /// Uses browser APIs to detect online/offline state and trigger queue processing
    /// </summary>
    public class ConnectivityService : IConnectivityService, IAsyncDisposable
    {
        private readonly IJSRuntimeWrapper _jsRuntime;
        private readonly IOfflineQueueService _queueService;
        private readonly ILogger<ConnectivityService>? _logger;
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
        
        private bool _isMonitoring = false;
        private bool _disposed = false;
        private bool? _lastKnownState = null;
        private DotNetObjectReference<ConnectivityService>? _objectReference;

        public ConnectivityService(
            IJSRuntimeWrapper jsRuntime, 
            IOfflineQueueService queueService,
            ILogger<ConnectivityService>? logger = null)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
            _logger = logger;

            _moduleTask = new Lazy<Task<IJSObjectReference>>(async () => 
                await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./js/connectivity.js"));
        }

        public ConnectivityService(
            IJSRuntime jsRuntime, 
            IOfflineQueueService queueService,
            ILogger<ConnectivityService>? logger = null)
            : this(new JSRuntimeWrapper(jsRuntime), queueService, logger)
        {
        }

        // Events
        public event EventHandler<ConnectivityEventArgs>? OnOnline;
        public event EventHandler<ConnectivityEventArgs>? OnOffline;

        public async Task<bool> IsOnlineAsync()
        {
            ThrowIfDisposed();

            try
            {
                var module = await _moduleTask.Value;
                var isOnline = await module.InvokeAsync<bool>("isOnline");
                _logger?.LogDebug("Current connectivity status: {IsOnline}", isOnline);
                return isOnline;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to check connectivity status");
                // Default to online if we can't determine the status
                return true;
            }
        }

        public async Task StartMonitoringAsync()
        {
            ThrowIfDisposed();

            if (_isMonitoring)
            {
                _logger?.LogWarning("Connectivity monitoring is already active");
                return;
            }

            try
            {
                _logger?.LogInformation("Starting connectivity monitoring");
                
                var module = await _moduleTask.Value;
                _objectReference = DotNetObjectReference.Create(this);
                
                // Get initial state
                _lastKnownState = await IsOnlineAsync();
                
                // Start monitoring
                await module.InvokeVoidAsync("startMonitoring", _objectReference);
                _isMonitoring = true;
                
                _logger?.LogInformation("Connectivity monitoring started, initial state: {IsOnline}", _lastKnownState);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start connectivity monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            ThrowIfDisposed();

            if (!_isMonitoring)
            {
                _logger?.LogWarning("Connectivity monitoring is not active");
                return;
            }

            try
            {
                _logger?.LogInformation("Stopping connectivity monitoring");
                
                var module = await _moduleTask.Value;
                await module.InvokeVoidAsync("stopMonitoring");
                
                _objectReference?.Dispose();
                _objectReference = null;
                _isMonitoring = false;
                
                _logger?.LogInformation("Connectivity monitoring stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to stop connectivity monitoring");
                throw;
            }
        }

        /// <summary>
        /// Called from JavaScript when connectivity changes to online
        /// </summary>
        [JSInvokable]
        public async Task OnConnectivityOnline()
        {
            if (_disposed) return;

            try
            {
                _logger?.LogInformation("Device went online");
                
                var args = new ConnectivityEventArgs
                {
                    IsOnline = true,
                    PreviousState = _lastKnownState
                };
                
                _lastKnownState = true;
                
                // Trigger queue processing when going online
                _logger?.LogInformation("Triggering offline queue processing due to connectivity restore");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _queueService.ProcessQueueAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to process offline queue after going online");
                    }
                });
                
                OnOnline?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling online event");
            }
        }

        /// <summary>
        /// Called from JavaScript when connectivity changes to offline
        /// </summary>
        [JSInvokable]
        public async Task OnConnectivityOffline()
        {
            if (_disposed) return;

            try
            {
                _logger?.LogInformation("Device went offline");
                
                var args = new ConnectivityEventArgs
                {
                    IsOnline = false,
                    PreviousState = _lastKnownState
                };
                
                _lastKnownState = false;
                OnOffline?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling offline event");
            }
        }

        // IDisposable Implementation
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;  // Mark as disposed immediately
                _ = Task.Run(async () => await DisposeAsync());
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Note: _disposed might already be set by Dispose() method
            if (_disposed && _isMonitoring == false && _objectReference == null) return;

            try
            {
                if (_isMonitoring)
                {
                    // Don't call StopMonitoringAsync as it checks ThrowIfDisposed
                    // Instead, directly clean up monitoring resources
                    try
                    {
                        if (_moduleTask.IsValueCreated)
                        {
                            var module = await _moduleTask.Value;
                            await module.InvokeVoidAsync("stopMonitoring");
                        }
                    }
                    catch { }
                    _isMonitoring = false;
                }

                if (_moduleTask.IsValueCreated)
                {
                    try
                    {
                        var module = await _moduleTask.Value;
                        await module.DisposeAsync();
                    }
                    catch { }
                }

                _objectReference?.Dispose();
                _objectReference = null;
                
                _logger?.LogDebug("ConnectivityService disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during ConnectivityService disposal");
            }
            finally
            {
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConnectivityService));
            }
        }
    }
}