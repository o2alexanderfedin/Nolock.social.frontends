using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Accessibility.Interfaces
{
    /// <summary>
    /// Service interface for voice command recognition and processing using Web Speech API
    /// </summary>
    public interface IVoiceCommandService
    {
        /// <summary>
        /// Event fired when a voice command is recognized and matched
        /// </summary>
        event EventHandler<VoiceCommandEventArgs> OnCommandRecognized;
        
        /// <summary>
        /// Event fired when speech recognition encounters an error
        /// </summary>
        event EventHandler<SpeechErrorEventArgs> OnSpeechError;
        
        /// <summary>
        /// Begins speech recognition listening session
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task StartListeningAsync();
        
        /// <summary>
        /// Ends the current speech recognition session
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task StopListeningAsync();
        
        /// <summary>
        /// Gets the current listening state of the speech recognition service
        /// </summary>
        /// <returns>True if currently listening for voice commands</returns>
        Task<bool> IsListeningAsync();
        
        /// <summary>
        /// Configures the voice commands and their associated actions
        /// </summary>
        /// <param name="commands">Dictionary mapping command phrases to their callback functions</param>
        /// <returns>Task representing the async operation</returns>
        Task SetCommandsAsync(Dictionary<string, Func<Task>> commands);
        
        /// <summary>
        /// Gets the currently configured voice commands
        /// </summary>
        /// <returns>Dictionary of configured commands</returns>
        Task<Dictionary<string, Func<Task>>> GetCommandsAsync();
        
        /// <summary>
        /// Checks if the browser supports Web Speech API
        /// </summary>
        /// <returns>True if speech recognition is supported</returns>
        Task<bool> IsSpeechRecognitionSupportedAsync();
    }
    
    /// <summary>
    /// Event arguments for voice command recognition events
    /// </summary>
    public class VoiceCommandEventArgs : EventArgs
    {
        public string RecognizedText { get; set; } = string.Empty;
        public string MatchedCommand { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Event arguments for speech recognition error events
    /// </summary>
    public class SpeechErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}