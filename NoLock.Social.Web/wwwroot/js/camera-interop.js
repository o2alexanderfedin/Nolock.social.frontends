// Camera interop functions for Blazor
window.cameraInterop = {
    stream: null,
    video: null,
    
    // Check camera permission status
    checkCameraPermission: async function() {
        try {
            // Check if permissions API is available
            if (navigator.permissions && navigator.permissions.query) {
                const result = await navigator.permissions.query({ name: 'camera' });
                return result.state; // 'granted', 'denied', or 'prompt'
            }
            // If permissions API not available, return 'prompt' to try getUserMedia
            return 'prompt';
        } catch (error) {
            console.log('Permissions API not available, will request on use');
            return 'prompt';
        }
    },
    
    // Request camera permission
    requestCameraPermission: async function() {
        try {
            // Request camera access by trying to get user media
            const stream = await navigator.mediaDevices.getUserMedia({ video: true });
            // If successful, stop the stream immediately (we just wanted permission)
            stream.getTracks().forEach(track => track.stop());
            return 'granted';
        } catch (error) {
            console.error('Camera permission denied:', error);
            return 'denied';
        }
    },
    
    setupCamera: async function(videoElementId, streamUrl) {
        try {
            const video = document.getElementById(videoElementId);
            if (!video) {
                console.error('Video element not found');
                return false;
            }
            
            this.video = video;
            
            // If streamUrl is provided, use it
            if (streamUrl) {
                video.src = streamUrl;
                return true;
            }
            
            // Otherwise, get user media
            const constraints = {
                video: {
                    width: { ideal: 1920 },
                    height: { ideal: 1080 },
                    facingMode: 'environment'
                },
                audio: false
            };
            
            this.stream = await navigator.mediaDevices.getUserMedia(constraints);
            video.srcObject = this.stream;
            return true;
        } catch (error) {
            console.error('Error setting up camera:', error);
            throw error;
        }
    },
    
    captureImage: function(canvasId, videoId) {
        try {
            const video = document.getElementById(videoId) || this.video;
            const canvas = document.getElementById(canvasId);
            
            if (!video || !canvas) {
                console.error('Video or canvas element not found');
                return null;
            }
            
            const context = canvas.getContext('2d');
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            context.drawImage(video, 0, 0, canvas.width, canvas.height);
            
            // Return base64 image
            return canvas.toDataURL('image/jpeg', 0.95);
        } catch (error) {
            console.error('Error capturing image:', error);
            return null;
        }
    },
    
    stopCamera: function() {
        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }
        if (this.video) {
            this.video.srcObject = null;
            // Don't null the video reference - we need it for switching cameras
            // this.video = null;
        }
    },
    
    getAvailableCameras: async function() {
        try {
            const devices = await navigator.mediaDevices.enumerateDevices();
            return devices
                .filter(device => device.kind === 'videoinput')
                .map(device => ({
                    id: device.deviceId,
                    label: device.label || `Camera ${device.deviceId.substring(0, 8)}`
                }));
        } catch (error) {
            console.error('Error getting cameras:', error);
            return [];
        }
    },
    
    switchCamera: async function(deviceId) {
        try {
            // Get video element if not already set
            if (!this.video) {
                this.video = document.getElementById('cameraPreview');
                if (!this.video) {
                    console.error('Video element not found');
                    return false;
                }
            }
            
            // Stop current stream and clean up
            if (this.stream) {
                this.stream.getTracks().forEach(track => track.stop());
                // Also pause the video to stop any pending operations
                if (this.video) {
                    this.video.pause();
                    this.video.srcObject = null;
                }
            }
            
            // Start new stream with selected camera
            const constraints = {
                video: {
                    deviceId: { exact: deviceId },
                    width: { ideal: 1920 },
                    height: { ideal: 1080 }
                },
                audio: false
            };
            
            this.stream = await navigator.mediaDevices.getUserMedia(constraints);
            
            // Set the new stream to video element
            if (this.video) {
                this.video.srcObject = this.stream;
                // Ensure video plays
                await this.video.play();
            }
            
            return true;
        } catch (error) {
            console.error('Error switching camera:', error);
            
            // Try to restart with any available camera if exact match fails
            try {
                const fallbackConstraints = {
                    video: {
                        width: { ideal: 1920 },
                        height: { ideal: 1080 }
                    },
                    audio: false
                };
                
                this.stream = await navigator.mediaDevices.getUserMedia(fallbackConstraints);
                
                if (this.video) {
                    this.video.srcObject = this.stream;
                    await this.video.play();
                }
            } catch (fallbackError) {
                console.error('Fallback camera also failed:', fallbackError);
            }
            
            throw error;
        }
    },
    
    setZoom: function(zoomLevel) {
        if (this.stream) {
            const track = this.stream.getVideoTracks()[0];
            const capabilities = track.getCapabilities();
            
            if (capabilities.zoom) {
                const settings = {
                    zoom: Math.min(Math.max(zoomLevel, capabilities.zoom.min), capabilities.zoom.max)
                };
                track.applyConstraints({ advanced: [settings] });
            }
        }
    },
    
    toggleTorch: function(enabled) {
        if (this.stream) {
            const track = this.stream.getVideoTracks()[0];
            const capabilities = track.getCapabilities();
            
            if (capabilities.torch) {
                track.applyConstraints({
                    advanced: [{ torch: enabled }]
                });
            }
        }
    }
};

// Export for use
window.checkCameraPermission = window.cameraInterop.checkCameraPermission.bind(window.cameraInterop);
window.requestCameraPermission = window.cameraInterop.requestCameraPermission.bind(window.cameraInterop);
window.setupCamera = window.cameraInterop.setupCamera.bind(window.cameraInterop);
window.captureImage = window.cameraInterop.captureImage.bind(window.cameraInterop);
window.stopCamera = window.cameraInterop.stopCamera.bind(window.cameraInterop);
window.getAvailableCameras = window.cameraInterop.getAvailableCameras.bind(window.cameraInterop);
window.switchCamera = window.cameraInterop.switchCamera.bind(window.cameraInterop);
window.setZoom = window.cameraInterop.setZoom.bind(window.cameraInterop);
window.toggleTorch = window.cameraInterop.toggleTorch.bind(window.cameraInterop);