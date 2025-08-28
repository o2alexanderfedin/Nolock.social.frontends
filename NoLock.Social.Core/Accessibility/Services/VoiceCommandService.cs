using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoLock.Social.Core.Accessibility.Interfaces;

namespace NoLock.Social.Core.Accessibility.Services
{
    /// <summary>
    /// Service implementation for voice command recognition using Web Speech API via JSInterop
    /// </summary>
    public class VoiceCommandService : IVoiceCommandService, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<VoiceCommandService> _logger;
        
        private Dictionary<string, Func<Task>> _commands = new();
        private bool _isListening;
        private bool _isDisposed;
        private DotNetObjectReference<VoiceCommandService>? _objectReference;
        
        public event EventHandler<VoiceCommandEventArgs>? OnCommandRecognized;
        public event EventHandler<SpeechErrorEventArgs>? OnSpeechError;
        
        public VoiceCommandService(IJSRuntime jsRuntime, ILogger<VoiceCommandService> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _objectReference = DotNetObjectReference.Create(this);
        }
        
        public async Task StartListeningAsync()
        {
            try
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(VoiceCommandService));
                    
                if (_isListening)
                {
                    _logger.LogWarning("Speech recognition is already listening");
                    return;
                }
                
                _logger.LogInformation("Starting speech recognition");
                
                await _jsRuntime.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.startListening", _objectReference);
                _isListening = true;
                
                _logger.LogInformation("Speech recognition started successfully");
            }
            catch (JSException ex)
            {
                _logger.LogError(ex, "Failed to start speech recognition");
                _isListening = false;
                
                var errorArgs = new SpeechErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    ErrorCode = "JS_EXCEPTION"
                };
                OnSpeechError?.Invoke(this, errorArgs);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error starting speech recognition");
                _isListening = false;
                throw;
            }
        }
        
        public async Task StopListeningAsync()
        {
            try
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(VoiceCommandService));
                    
                if (!_isListening)
                {
                    _logger.LogWarning("Speech recognition is not currently listening");
                    return;
                }
                
                _logger.LogInformation("Stopping speech recognition");
                
                await _jsRuntime.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("speechRecognition.stopListening");
                _isListening = false;
                
                _logger.LogInformation("Speech recognition stopped successfully");
            }
            catch (JSException ex)
            {
                _logger.LogError(ex, "Failed to stop speech recognition");
                
                var errorArgs = new SpeechErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    ErrorCode = "JS_EXCEPTION"
                };
                OnSpeechError?.Invoke(this, errorArgs);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error stopping speech recognition");
                throw;
            }
        }
        
        public Task<bool> IsListeningAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VoiceCommandService));
                
            return Task.FromResult(_isListening);
        }
        
        public Task SetCommandsAsync(Dictionary<string, Func<Task>> commands)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VoiceCommandService));
                
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));
            
            _commands = new Dictionary<string, Func<Task>>(commands, StringComparer.OrdinalIgnoreCase);
            
            _logger.LogInformation("Voice commands configured: {CommandCount} commands", _commands.Count);
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                foreach (var command in _commands.Keys)
                {
                    _logger.LogDebug("Registered voice command: {Command}", command);
                }
            }
            
            return Task.CompletedTask;
        }
        
        public Task<Dictionary<string, Func<Task>>> GetCommandsAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VoiceCommandService));
                
            return Task.FromResult(new Dictionary<string, Func<Task>>(_commands, StringComparer.OrdinalIgnoreCase));
        }
        
        public async Task<bool> IsSpeechRecognitionSupportedAsync()
        {
            try
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(VoiceCommandService));
                    
                var isSupported = await _jsRuntime.InvokeAsync<bool>("speechRecognition.isSupported");
                
                _logger.LogInformation("Speech recognition support check: {IsSupported}", isSupported);
                
                return isSupported;
            }
            catch (JSException ex)
            {
                _logger.LogError(ex, "Failed to check speech recognition support");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error checking speech recognition support");
                return false;
            }
        }
        
        /// <summary>
        /// JSInterop callback method for speech recognition results
        /// </summary>
        [JSInvokable]
        public async Task OnSpeechRecognized(string recognizedText, double confidence)
        {
            try
            {
                if (_isDisposed || string.IsNullOrWhiteSpace(recognizedText))
                    return;
                
                _logger.LogDebug("Speech recognized: '{Text}' (confidence: {Confidence:F2})", 
                    recognizedText, confidence);
                
                // Find matching command using case-insensitive partial matching
                var matchedCommand = FindMatchingCommand(recognizedText);
                
                if (matchedCommand.HasValue)
                {
                    var command = matchedCommand.Value;
                    _logger.LogInformation("Voice command matched: '{Command}' from text '{Text}'", 
                        command.Key, recognizedText);
                    
                    var eventArgs = new VoiceCommandEventArgs
                    {
                        RecognizedText = recognizedText,
                        MatchedCommand = command.Key,
                        Confidence = confidence
                    };
                    
                    OnCommandRecognized?.Invoke(this, eventArgs);
                    
                    // Execute the command
                    try
                    {
                        await command.Value();
                        _logger.LogInformation("Voice command executed successfully: {Command}", command.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute voice command: {Command}", command.Key);
                        
                        var errorArgs = new SpeechErrorEventArgs
                        {
                            ErrorMessage = $"Command execution failed: {ex.Message}",
                            ErrorCode = "COMMAND_EXECUTION_ERROR"
                        };
                        OnSpeechError?.Invoke(this, errorArgs);
                    }
                }
                else
                {
                    _logger.LogDebug("No matching voice command found for: '{Text}'", recognizedText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing speech recognition result");
                
                var errorArgs = new SpeechErrorEventArgs
                {
                    ErrorMessage = $"Processing error: {ex.Message}",
                    ErrorCode = "PROCESSING_ERROR"
                };
                OnSpeechError?.Invoke(this, errorArgs);
            }
        }
        
        /// <summary>
        /// JSInterop callback method for speech recognition errors
        /// </summary>
        [JSInvokable]
        public Task HandleSpeechError(string errorMessage, string errorCode)
        {
            try
            {
                _logger.LogError("Speech recognition error: {ErrorCode} - {ErrorMessage}", errorCode, errorMessage);
                
                _isListening = false;
                
                var errorArgs = new SpeechErrorEventArgs
                {
                    ErrorMessage = errorMessage,
                    ErrorCode = errorCode
                };
                
                OnSpeechError?.Invoke(this, errorArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling speech recognition error callback");
            }
            
            return Task.CompletedTask;
        }
        
        private KeyValuePair<string, Func<Task>>? FindMatchingCommand(string recognizedText)
        {
            var normalizedText = recognizedText.ToLowerInvariant().Trim();
            
            // First, try exact matches
            foreach (var command in _commands)
            {
                if (string.Equals(command.Key, normalizedText, StringComparison.OrdinalIgnoreCase))
                {
                    return command;
                }
            }
            
            // Then try partial matches (command keywords contained in recognized text)
            foreach (var command in _commands)
            {
                var commandKeywords = command.Key.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                if (commandKeywords.All(keyword => normalizedText.Contains(keyword)))
                {
                    return command;
                }
            }
            
            return null;
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;
            
            try
            {
                if (_isListening)
                {
                    await StopListeningAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping speech recognition during disposal");
            }
            
            _objectReference?.Dispose();
            _commands.Clear();
            _isDisposed = true;
            
            _logger.LogInformation("VoiceCommandService disposed");
        }
    }
}