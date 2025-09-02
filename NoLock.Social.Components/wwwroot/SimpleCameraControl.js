// JavaScript module for SimpleCameraControl component
let currentStream = null;

export async function startCamera(videoElement, deviceId) {
    try {
        // Stop any existing stream
        if (currentStream) {
            stopCamera();
        }

        // Build constraints
        const constraints = {
            video: {
                facingMode: deviceId ? undefined : 'user',
                deviceId: deviceId ? { exact: deviceId } : undefined,
                width: { ideal: 1920 },
                height: { ideal: 1080 }
            },
            audio: false
        };

        // Request camera access
        currentStream = await navigator.mediaDevices.getUserMedia(constraints);
        
        // Attach stream to video element
        if (videoElement) {
            videoElement.srcObject = currentStream;
            await videoElement.play();
        }
        
        console.log('Camera started successfully');
        return true;
    } catch (error) {
        console.error('Failed to start camera:', error);
        return false;
    }
}

export function stopCamera() {
    if (currentStream) {
        // Stop all tracks in the stream
        currentStream.getTracks().forEach(track => {
            track.stop();
        });
        currentStream = null;
        console.log('Camera stopped');
    }
    return true;
}

export async function getCameraDevices() {
    try {
        // Request permissions first if needed
        if (!currentStream) {
            const tempStream = await navigator.mediaDevices.getUserMedia({ video: true });
            tempStream.getTracks().forEach(track => track.stop());
        }

        // Get all devices
        const devices = await navigator.mediaDevices.enumerateDevices();
        
        // Filter for video input devices
        const cameras = devices
            .filter(device => device.kind === 'videoinput')
            .map(device => ({
                deviceId: device.deviceId,
                label: device.label || `Camera ${device.deviceId.substring(0, 8)}`,
                groupId: device.groupId
            }));
        
        console.log(`Found ${cameras.length} camera(s)`);
        return cameras;
    } catch (error) {
        console.error('Failed to enumerate camera devices:', error);
        return [];
    }
}

export function captureImage(videoElement) {
    try {
        if (!videoElement || !currentStream) {
            console.error('No video element or stream available');
            return null;
        }

        // Create canvas with video dimensions
        const canvas = document.createElement('canvas');
        canvas.width = videoElement.videoWidth;
        canvas.height = videoElement.videoHeight;
        
        // Draw current video frame to canvas
        const context = canvas.getContext('2d');
        context.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
        
        // Convert to base64 JPEG
        const dataUrl = canvas.toDataURL('image/jpeg', 0.92);
        
        console.log('Image captured successfully');
        return dataUrl;
    } catch (error) {
        console.error('Failed to capture image:', error);
        return null;
    }
}