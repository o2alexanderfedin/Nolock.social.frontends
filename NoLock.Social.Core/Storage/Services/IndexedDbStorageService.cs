using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.Storage.Interfaces;
using System.Text.Json;

namespace NoLock.Social.Core.Storage.Services
{
    public class IndexedDbStorageService : IOfflineStorageService, IDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _disposed = false;
        private bool _isInitialized = false;

        public IndexedDbStorageService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                try
                {
                    await _jsRuntime.InvokeVoidAsync("indexedDbStorage.initialize");
                    _isInitialized = true;
                }
                catch (JSException ex)
                {
                    throw new OfflineStorageException("Failed to initialize IndexedDB storage", "initialize", ex);
                }
            }
        }

        public async Task SaveSessionAsync(DocumentSession session)
        {
            ThrowIfDisposed();
            
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            await EnsureInitializedAsync();

            try
            {
                var sessionJson = JsonSerializer.Serialize(session);
                await _jsRuntime.InvokeVoidAsync("indexedDbStorage.saveSession", session.SessionId, sessionJson);
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException($"Failed to save session '{session.SessionId}'", "saveSession", ex);
            }
        }

        public async Task<DocumentSession?> LoadSessionAsync(string sessionId)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            await EnsureInitializedAsync();

            try
            {
                var sessionJson = await _jsRuntime.InvokeAsync<string>("indexedDbStorage.loadSession", sessionId);
                
                if (string.IsNullOrEmpty(sessionJson))
                    return null;

                return JsonSerializer.Deserialize<DocumentSession>(sessionJson);
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException($"Failed to load session '{sessionId}'", "loadSession", ex);
            }
            catch (JsonException ex)
            {
                throw new OfflineStorageException($"Failed to deserialize session '{sessionId}'", "loadSession", ex);
            }
        }

        public async Task SaveImageAsync(CapturedImage image)
        {
            ThrowIfDisposed();
            
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            await EnsureInitializedAsync();

            try
            {
                var imageJson = JsonSerializer.Serialize(image);
                var imageId = image.Timestamp.ToString("yyyyMMddHHmmssfff");
                await _jsRuntime.InvokeVoidAsync("indexedDbStorage.saveImage", imageId, imageJson);
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException($"Failed to save image", "saveImage", ex);
            }
        }

        public async Task<CapturedImage?> LoadImageAsync(string imageId)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(imageId))
                throw new ArgumentException("Image ID cannot be null or empty", nameof(imageId));

            await EnsureInitializedAsync();

            try
            {
                var imageJson = await _jsRuntime.InvokeAsync<string>("indexedDbStorage.loadImage", imageId);
                
                if (string.IsNullOrEmpty(imageJson))
                    return null;

                return JsonSerializer.Deserialize<CapturedImage>(imageJson);
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException($"Failed to load image '{imageId}'", "loadImage", ex);
            }
            catch (JsonException ex)
            {
                throw new OfflineStorageException($"Failed to deserialize image '{imageId}'", "loadImage", ex);
            }
        }

        public async Task QueueOfflineOperationAsync(OfflineOperation operation)
        {
            ThrowIfDisposed();
            
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await EnsureInitializedAsync();

            try
            {
                var operationJson = JsonSerializer.Serialize(operation);
                await _jsRuntime.InvokeVoidAsync("indexedDbStorage.queueOperation", operation.OperationId, operationJson);
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException($"Failed to queue operation '{operation.OperationId}'", "queueOperation", ex);
            }
        }

        public async Task<List<OfflineOperation>> GetPendingOperationsAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();

            try
            {
                var operationsJson = await _jsRuntime.InvokeAsync<string[]>("indexedDbStorage.getPendingOperations");
                var operations = new List<OfflineOperation>();

                if (operationsJson != null)
                {
                    foreach (var operationJson in operationsJson)
                    {
                        if (!string.IsNullOrEmpty(operationJson))
                        {
                            var operation = JsonSerializer.Deserialize<OfflineOperation>(operationJson);
                            if (operation != null)
                            {
                                operations.Add(operation);
                            }
                        }
                    }
                }

                return operations;
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException("Failed to get pending operations", "getPendingOperations", ex);
            }
            catch (JsonException ex)
            {
                throw new OfflineStorageException("Failed to deserialize pending operations", "getPendingOperations", ex);
            }
        }

        public async Task RemoveOperationAsync(string operationId)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(operationId))
                throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));

            await EnsureInitializedAsync();

            try
            {
                await _jsRuntime.InvokeVoidAsync("indexedDbStorage.removeOperation", operationId);
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException($"Failed to remove operation '{operationId}'", "removeOperation", ex);
            }
        }

        public async Task ClearAllDataAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();

            try
            {
                await _jsRuntime.InvokeVoidAsync("indexedDbStorage.clearAllData");
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException("Failed to clear all data", "clearAllData", ex);
            }
        }

        public async Task<List<DocumentSession>> GetAllSessionsAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();

            try
            {
                var sessionsJson = await _jsRuntime.InvokeAsync<string[]>("indexedDbStorage.getAllSessions");
                var sessions = new List<DocumentSession>();

                if (sessionsJson != null)
                {
                    foreach (var sessionJson in sessionsJson)
                    {
                        if (!string.IsNullOrEmpty(sessionJson))
                        {
                            var session = JsonSerializer.Deserialize<DocumentSession>(sessionJson);
                            if (session != null)
                            {
                                sessions.Add(session);
                            }
                        }
                    }
                }

                return sessions;
            }
            catch (JSException ex)
            {
                throw new OfflineStorageException("Failed to get all sessions", "getAllSessions", ex);
            }
            catch (JsonException ex)
            {
                throw new OfflineStorageException("Failed to deserialize sessions", "getAllSessions", ex);
            }
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
                        // Note: Cannot await in Dispose, but JS calls should be fire-and-forget for cleanup
                        if (_isInitialized)
                        {
                            _jsRuntime.InvokeVoidAsync("indexedDbStorage.dispose");
                        }
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
                throw new ObjectDisposedException(nameof(IndexedDbStorageService));
            }
        }
    }
}