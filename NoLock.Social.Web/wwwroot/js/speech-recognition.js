// Speech Recognition JavaScript Module for Voice Commands
// Handles Web Speech API integration with browser compatibility

window.speechRecognition = (function() {
    'use strict';
    
    let recognition = null;
    let dotNetHelper = null;
    let isListening = false;
    let reconnectTimeout = null;
    let maxReconnectAttempts = 3;
    let reconnectAttempts = 0;
    
    // Browser compatibility check
    function isSupported() {
        return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
    }
    
    // Initialize speech recognition with configuration
    function initializeRecognition() {
        if (!isSupported()) {
            console.error('Speech recognition not supported in this browser');
            return null;
        }
        
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        const recognitionInstance = new SpeechRecognition();
        
        // Configuration for continuous listening with voice commands
        recognitionInstance.continuous = true;
        recognitionInstance.interimResults = false;
        recognitionInstance.lang = 'en-US';
        recognitionInstance.maxAlternatives = 1;
        
        // Set up event handlers
        recognitionInstance.onstart = function() {
            console.log('Speech recognition started');
            isListening = true;
            reconnectAttempts = 0;
        };
        
        recognitionInstance.onend = function() {
            console.log('Speech recognition ended');
            isListening = false;
            
            // Auto-restart if we're supposed to be listening (handles browser timeout)
            if (recognition && reconnectAttempts < maxReconnectAttempts) {
                console.log('Attempting to restart speech recognition...');
                reconnectTimeout = setTimeout(() => {
                    if (recognition) {
                        reconnectAttempts++;
                        try {
                            recognition.start();
                        } catch (error) {
                            console.error('Failed to restart speech recognition:', error);
                            handleError('RESTART_FAILED', error.message);
                        }
                    }
                }, 1000);
            }
        };
        
        recognitionInstance.onerror = function(event) {
            console.error('Speech recognition error:', event.error);
            isListening = false;
            
            let errorCode = 'UNKNOWN_ERROR';
            let errorMessage = event.error;
            
            switch (event.error) {
                case 'no-speech':
                    errorCode = 'NO_SPEECH';
                    errorMessage = 'No speech was detected';
                    break;
                case 'audio-capture':
                    errorCode = 'AUDIO_CAPTURE';
                    errorMessage = 'Audio capture failed';
                    break;
                case 'not-allowed':
                    errorCode = 'NOT_ALLOWED';
                    errorMessage = 'Speech recognition permission denied';
                    break;
                case 'network':
                    errorCode = 'NETWORK_ERROR';
                    errorMessage = 'Network error occurred';
                    break;
                case 'service-not-allowed':
                    errorCode = 'SERVICE_NOT_ALLOWED';
                    errorMessage = 'Speech recognition service not allowed';
                    break;
                case 'bad-grammar':
                    errorCode = 'BAD_GRAMMAR';
                    errorMessage = 'Grammar compilation failed';
                    break;
                case 'language-not-supported':
                    errorCode = 'LANGUAGE_NOT_SUPPORTED';
                    errorMessage = 'Language not supported';
                    break;
            }
            
            handleError(errorCode, errorMessage);
        };
        
        recognitionInstance.onresult = function(event) {
            try {
                const lastResultIndex = event.results.length - 1;
                const result = event.results[lastResultIndex];
                
                if (result.isFinal) {
                    const transcript = result[0].transcript.trim();
                    const confidence = result[0].confidence || 0.0;
                    
                    console.log('Speech recognized:', transcript, 'Confidence:', confidence);
                    
                    if (dotNetHelper && transcript) {
                        dotNetHelper.invokeMethodAsync('OnSpeechRecognized', transcript, confidence)
                            .catch(error => {
                                console.error('Failed to invoke .NET callback:', error);
                                handleError('DOTNET_CALLBACK_ERROR', error.message);
                            });
                    }
                }
            } catch (error) {
                console.error('Error processing speech result:', error);
                handleError('RESULT_PROCESSING_ERROR', error.message);
            }
        };
        
        return recognitionInstance;
    }
    
    function handleError(errorCode, errorMessage) {
        if (dotNetHelper) {
            dotNetHelper.invokeMethodAsync('HandleSpeechError', errorMessage, errorCode)
                .catch(error => {
                    console.error('Failed to invoke .NET error callback:', error);
                });
        }
    }
    
    function startListening(dotNetHelperReference) {
        try {
            if (!isSupported()) {
                throw new Error('Speech recognition not supported in this browser');
            }
            
            if (isListening) {
                console.warn('Speech recognition is already listening');
                return;
            }
            
            dotNetHelper = dotNetHelperReference;
            
            if (!recognition) {
                recognition = initializeRecognition();
                if (!recognition) {
                    throw new Error('Failed to initialize speech recognition');
                }
            }
            
            // Clear any pending reconnect timeouts
            if (reconnectTimeout) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }
            
            recognition.start();
            console.log('Starting speech recognition...');
            
        } catch (error) {
            console.error('Failed to start speech recognition:', error);
            isListening = false;
            handleError('START_ERROR', error.message);
            throw error;
        }
    }
    
    function stopListening() {
        try {
            if (!isListening || !recognition) {
                console.warn('Speech recognition is not currently listening');
                return;
            }
            
            // Clear reconnect timeout to prevent auto-restart
            if (reconnectTimeout) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }
            
            recognition.stop();
            recognition = null;
            dotNetHelper = null;
            reconnectAttempts = maxReconnectAttempts; // Prevent auto-restart
            
            console.log('Speech recognition stopped');
            
        } catch (error) {
            console.error('Failed to stop speech recognition:', error);
            handleError('STOP_ERROR', error.message);
            throw error;
        }
    }
    
    // Request microphone permissions
    function requestMicrophonePermission() {
        return navigator.mediaDevices.getUserMedia({ audio: true })
            .then(function(stream) {
                // Stop the stream immediately, we just needed permission
                stream.getTracks().forEach(track => track.stop());
                return true;
            })
            .catch(function(error) {
                console.error('Microphone permission denied:', error);
                return false;
            });
    }
    
    // Check current permission status
    function checkMicrophonePermission() {
        if (!navigator.permissions) {
            return Promise.resolve('prompt'); // Unknown, assume prompt
        }
        
        return navigator.permissions.query({ name: 'microphone' })
            .then(function(permission) {
                return permission.state; // 'granted', 'denied', or 'prompt'
            })
            .catch(function() {
                return 'prompt'; // Fallback
            });
    }
    
    // Public API
    return {
        isSupported: isSupported,
        startListening: startListening,
        stopListening: stopListening,
        requestMicrophonePermission: requestMicrophonePermission,
        checkMicrophonePermission: checkMicrophonePermission,
        isListening: function() { return isListening; }
    };
})();