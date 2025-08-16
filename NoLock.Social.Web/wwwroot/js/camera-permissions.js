// Camera permissions JavaScript interop for Blazor
window.cameraPermissions = {
    // Get current camera permission state
    getState: async function() {
        try {
            // Check if permissions API is available
            if ('permissions' in navigator) {
                const permission = await navigator.permissions.query({ name: 'camera' });
                
                // Map permission state to our enum values
                switch(permission.state) {
                    case 'granted':
                        return 'Granted';
                    case 'denied':
                        return 'Denied';
                    case 'prompt':
                        return 'Prompt';
                    default:
                        return 'NotRequested';
                }
            }
            
            // Fallback: check if MediaDevices API is available
            if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
                // We can't determine state without requesting, return NotRequested
                return 'NotRequested';
            }
            
            // No camera API support
            return 'Denied';
        } catch (error) {
            console.error('Error checking camera permission:', error);
            return 'NotRequested';
        }
    },
    
    // Request camera permission
    request: async function() {
        try {
            // Check if MediaDevices API is available
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                console.error('Camera API not supported in this browser');
                return 'Denied';
            }
            
            // Request camera access
            const stream = await navigator.mediaDevices.getUserMedia({ 
                video: true,
                audio: false 
            });
            
            // Stop the stream immediately (we just needed permission)
            stream.getTracks().forEach(track => track.stop());
            
            return 'Granted';
        } catch (error) {
            // Handle different error types
            if (error.name === 'NotAllowedError' || error.name === 'PermissionDeniedError') {
                return 'Denied';
            } else if (error.name === 'NotFoundError' || error.name === 'DevicesNotFoundError') {
                console.error('No camera device found');
                return 'Denied';
            } else {
                console.error('Error requesting camera permission:', error);
                return 'Denied';
            }
        }
    }
};