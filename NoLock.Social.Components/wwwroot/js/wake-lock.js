// Wake Lock API JavaScript Interop for Blazor
// Provides screen wake lock functionality with visibility monitoring

window.wakeLockInterop = (() => {
    let wakeLock = null;
    let visibilityListener = null;
    let dotNetObjectRef = null;

    return {
        /**
         * Acquire a screen wake lock to prevent the device from sleeping
         * @returns {Promise<boolean>} True if wake lock acquired successfully
         */
        async acquireWakeLock() {
            try {
                // Check if Wake Lock API is supported
                if (!('wakeLock' in navigator)) {
                    console.warn('Wake Lock API is not supported in this browser');
                    return false;
                }

                // Release existing wake lock if any
                if (wakeLock) {
                    await this.releaseWakeLock();
                }

                // Request screen wake lock
                wakeLock = await navigator.wakeLock.request('screen');
                console.log('Screen wake lock acquired');

                // Listen for wake lock release (can happen automatically)
                wakeLock.addEventListener('release', () => {
                    console.log('Wake lock was released');
                    wakeLock = null;
                });

                return true;
            } catch (error) {
                console.error('Failed to acquire wake lock:', error);
                wakeLock = null;
                return false;
            }
        },

        /**
         * Release the current wake lock if active
         * @returns {Promise<boolean>} True if wake lock released successfully
         */
        async releaseWakeLock() {
            try {
                if (wakeLock) {
                    await wakeLock.release();
                    wakeLock = null;
                    console.log('Wake lock released manually');
                }
                return true;
            } catch (error) {
                console.error('Failed to release wake lock:', error);
                return false;
            }
        },

        /**
         * Check if Wake Lock API is supported in the current browser
         * @returns {boolean} True if Wake Lock API is available
         */
        isWakeLockSupported() {
            return 'wakeLock' in navigator;
        },

        /**
         * Start monitoring page visibility changes and notify C# when visibility changes
         * @param {object} objRef - DotNet object reference for callbacks
         * @returns {boolean} True if monitoring started successfully
         */
        startVisibilityMonitoring(objRef) {
            try {
                // Store the DotNet object reference
                dotNetObjectRef = objRef;

                // Remove existing listener if any
                this.stopVisibilityMonitoring();

                // Create visibility change listener
                visibilityListener = async () => {
                    const isVisible = document.visibilityState === 'visible';
                    
                    try {
                        // Notify C# about visibility change
                        if (dotNetObjectRef) {
                            await DotNet.invokeMethodAsync('NoLock.Social.Core', 'OnVisibilityChanged', isVisible);
                        }

                        // Handle wake lock based on visibility
                        if (isVisible && wakeLock === null) {
                            // Page became visible and we don't have an active wake lock
                            console.log('Page became visible, attempting to reacquire wake lock');
                            await this.acquireWakeLock();
                        }
                    } catch (error) {
                        console.error('Error handling visibility change:', error);
                    }
                };

                // Add the event listener
                document.addEventListener('visibilitychange', visibilityListener);
                console.log('Visibility monitoring started');
                return true;
            } catch (error) {
                console.error('Failed to start visibility monitoring:', error);
                return false;
            }
        },

        /**
         * Stop monitoring page visibility changes
         * @returns {boolean} True if monitoring stopped successfully
         */
        stopVisibilityMonitoring() {
            try {
                if (visibilityListener) {
                    document.removeEventListener('visibilitychange', visibilityListener);
                    visibilityListener = null;
                    console.log('Visibility monitoring stopped');
                }
                
                // Clear the DotNet object reference
                dotNetObjectRef = null;
                return true;
            } catch (error) {
                console.error('Failed to stop visibility monitoring:', error);
                return false;
            }
        },

        /**
         * Get the current wake lock status
         * @returns {boolean} True if wake lock is currently active
         */
        isWakeLockActive() {
            return wakeLock !== null;
        },

        /**
         * Get current page visibility state
         * @returns {boolean} True if page is currently visible
         */
        isPageVisible() {
            return document.visibilityState === 'visible';
        },

        /**
         * Clean up all resources (called on disposal)
         * @returns {Promise<boolean>} True if cleanup completed successfully
         */
        async dispose() {
            try {
                await this.releaseWakeLock();
                this.stopVisibilityMonitoring();
                console.log('Wake lock interop disposed');
                return true;
            } catch (error) {
                console.error('Error during wake lock interop disposal:', error);
                return false;
            }
        }
    };
})();