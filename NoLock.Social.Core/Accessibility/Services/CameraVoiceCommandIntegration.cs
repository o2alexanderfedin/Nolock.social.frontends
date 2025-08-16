using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Accessibility.Interfaces;
using NoLock.Social.Core.Camera.Interfaces;

namespace NoLock.Social.Core.Accessibility.Services
{
    /// <summary>
    /// Integration service that connects voice commands with camera operations
    /// Demonstrates how to configure voice commands for camera control
    /// </summary>
    public class CameraVoiceCommandIntegration : IAsyncDisposable
    {
        private readonly IVoiceCommandService _voiceCommandService;
        private readonly ICameraService _cameraService;
        private readonly ILogger<CameraVoiceCommandIntegration> _logger;
        
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        
        public CameraVoiceCommandIntegration(
            IVoiceCommandService voiceCommandService,
            ICameraService cameraService,
            ILogger<CameraVoiceCommandIntegration> logger)
        {
            _voiceCommandService = voiceCommandService ?? throw new ArgumentNullException(nameof(voiceCommandService));
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Initializes voice commands for camera operations
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CameraVoiceCommandIntegration));
                
            if (_isInitialized)
            {
                _logger.LogWarning("Camera voice command integration already initialized");
                return;
            }
            
            try
            {
                _logger.LogInformation("Initializing camera voice command integration");
                
                // Configure voice commands for camera operations
                var commands = new Dictionary<string, Func<Task>>
                {
                    // Image capture commands
                    { "capture", CaptureImageAsync },
                    { "take photo", CaptureImageAsync },
                    { "snap", CaptureImageAsync },
                    { "take picture", CaptureImageAsync },
                    
                    // Camera stream control
                    { "start camera", StartCameraAsync },
                    { "begin scanning", StartCameraAsync },
                    { "activate camera", StartCameraAsync },
                    { "stop camera", StopCameraAsync },
                    { "end scanning", StopCameraAsync },
                    { "deactivate camera", StopCameraAsync },
                    
                    // Torch/flash control
                    { "torch on", () => ToggleTorchAsync(true) },
                    { "flash on", () => ToggleTorchAsync(true) },
                    { "light on", () => ToggleTorchAsync(true) },
                    { "torch off", () => ToggleTorchAsync(false) },
                    { "flash off", () => ToggleTorchAsync(false) },
                    { "light off", () => ToggleTorchAsync(false) },
                    
                    // Zoom control
                    { "zoom in", ZoomInAsync },
                    { "zoom out", ZoomOutAsync },
                    { "reset zoom", ResetZoomAsync },
                    
                    // Camera switching
                    { "switch camera", SwitchCameraAsync },
                    { "flip camera", SwitchCameraAsync },
                    { "change camera", SwitchCameraAsync }
                };
                
                await _voiceCommandService.SetCommandsAsync(commands);
                
                // Subscribe to voice command events
                _voiceCommandService.OnCommandRecognized += OnVoiceCommandRecognized;
                _voiceCommandService.OnSpeechError += OnSpeechError;
                
                _isInitialized = true;
                _logger.LogInformation("Camera voice command integration initialized with {CommandCount} commands", commands.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize camera voice command integration");
                throw;
            }
        }
        
        /// <summary>
        /// Starts listening for voice commands
        /// </summary>
        public async Task StartListeningAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CameraVoiceCommandIntegration));
                
            if (!_isInitialized)
                await InitializeAsync();
            
            try
            {
                _logger.LogInformation("Starting voice command listening for camera operations");
                await _voiceCommandService.StartListeningAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start voice command listening");
                throw;
            }
        }
        
        /// <summary>
        /// Stops listening for voice commands
        /// </summary>
        public async Task StopListeningAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CameraVoiceCommandIntegration));
                
            try
            {
                _logger.LogInformation("Stopping voice command listening");
                await _voiceCommandService.StopListeningAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop voice command listening");
                throw;
            }
        }
        
        // Camera operation implementations
        private async Task CaptureImageAsync()
        {
            try
            {
                _logger.LogInformation("Executing voice command: Capture image");
                await _cameraService.CaptureImageAsync();
                _logger.LogInformation("Image captured successfully via voice command");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture image via voice command");
                throw;
            }
        }
        
        private async Task StartCameraAsync()
        {
            try
            {
                _logger.LogInformation("Executing voice command: Start camera");
                await _cameraService.StartStreamAsync();
                _logger.LogInformation("Camera started successfully via voice command");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start camera via voice command");
                throw;
            }
        }
        
        private async Task StopCameraAsync()
        {
            try
            {
                _logger.LogInformation("Executing voice command: Stop camera");
                await _cameraService.StopStreamAsync();
                _logger.LogInformation("Camera stopped successfully via voice command");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop camera via voice command");
                throw;
            }
        }
        
        private async Task ToggleTorchAsync(bool enabled)
        {
            try
            {
                _logger.LogInformation("Executing voice command: Toggle torch {State}", enabled ? "on" : "off");
                await _cameraService.ToggleTorchAsync(enabled);
                _logger.LogInformation("Torch toggled {State} successfully via voice command", enabled ? "on" : "off");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle torch via voice command");
                throw;
            }
        }
        
        private async Task ZoomInAsync()
        {
            try
            {
                _logger.LogInformation("Executing voice command: Zoom in");
                var currentZoom = await _cameraService.GetZoomAsync();
                var newZoom = Math.Min(currentZoom + 0.5, 3.0); // Max zoom level of 3x
                await _cameraService.SetZoomAsync(newZoom);
                _logger.LogInformation("Zoom increased to {ZoomLevel} via voice command", newZoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to zoom in via voice command");
                throw;
            }
        }
        
        private async Task ZoomOutAsync()
        {
            try
            {
                _logger.LogInformation("Executing voice command: Zoom out");
                var currentZoom = await _cameraService.GetZoomAsync();
                var newZoom = Math.Max(currentZoom - 0.5, 1.0); // Min zoom level of 1x
                await _cameraService.SetZoomAsync(newZoom);
                _logger.LogInformation("Zoom decreased to {ZoomLevel} via voice command", newZoom);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to zoom out via voice command");
                throw;
            }
        }
        
        private async Task ResetZoomAsync()
        {
            try
            {
                _logger.LogInformation("Executing voice command: Reset zoom");
                await _cameraService.SetZoomAsync(1.0);
                _logger.LogInformation("Zoom reset to 1.0 via voice command");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset zoom via voice command");
                throw;
            }
        }
        
        private async Task SwitchCameraAsync()
        {
            try
            {
                _logger.LogInformation("Executing voice command: Switch camera");
                var availableCameras = await _cameraService.GetAvailableCamerasAsync();
                
                if (availableCameras.Length > 1)
                {
                    // Simple implementation: switch to the next available camera
                    // In a real implementation, you might want to track the current camera
                    await _cameraService.SwitchCameraAsync(availableCameras[1]);
                    _logger.LogInformation("Camera switched successfully via voice command");
                }
                else
                {
                    _logger.LogWarning("Cannot switch camera: only one camera available");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch camera via voice command");
                throw;
            }
        }
        
        // Event handlers
        private void OnVoiceCommandRecognized(object? sender, VoiceCommandEventArgs e)
        {
            _logger.LogInformation("Voice command recognized: '{Command}' from '{Text}' (confidence: {Confidence:F2})",
                e.MatchedCommand, e.RecognizedText, e.Confidence);
        }
        
        private void OnSpeechError(object? sender, SpeechErrorEventArgs e)
        {
            _logger.LogError("Speech recognition error: {ErrorCode} - {ErrorMessage}", e.ErrorCode, e.ErrorMessage);
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;
            
            try
            {
                if (_isInitialized)
                {
                    _voiceCommandService.OnCommandRecognized -= OnVoiceCommandRecognized;
                    _voiceCommandService.OnSpeechError -= OnSpeechError;
                    
                    if (await _voiceCommandService.IsListeningAsync())
                    {
                        await _voiceCommandService.StopListeningAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during camera voice command integration disposal");
            }
            
            _isDisposed = true;
            _logger.LogInformation("Camera voice command integration disposed");
        }
    }
}