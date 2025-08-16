/**
 * Browser connectivity monitoring service
 * Provides online/offline detection using navigator.onLine and connection events
 */

let dotNetReference = null;
let isMonitoring = false;

/**
 * Check if the browser is currently online
 * @returns {boolean} true if online, false if offline
 */
export function isOnline() {
    try {
        return navigator.onLine;
    } catch (error) {
        console.error('Error checking online status:', error);
        // Default to online if unable to determine
        return true;
    }
}

/**
 * Start monitoring connectivity changes
 * @param {object} dotNetRef - .NET object reference for callbacks
 */
export function startMonitoring(dotNetRef) {
    if (isMonitoring) {
        console.warn('Connectivity monitoring is already active');
        return;
    }

    try {
        dotNetReference = dotNetRef;
        
        // Add event listeners for online/offline events
        window.addEventListener('online', handleOnline);
        window.addEventListener('offline', handleOffline);
        
        isMonitoring = true;
        console.log('Connectivity monitoring started, current status:', navigator.onLine);
    } catch (error) {
        console.error('Error starting connectivity monitoring:', error);
        throw error;
    }
}

/**
 * Stop monitoring connectivity changes
 */
export function stopMonitoring() {
    if (!isMonitoring) {
        console.warn('Connectivity monitoring is not active');
        return;
    }

    try {
        // Remove event listeners
        window.removeEventListener('online', handleOnline);
        window.removeEventListener('offline', handleOffline);
        
        dotNetReference = null;
        isMonitoring = false;
        console.log('Connectivity monitoring stopped');
    } catch (error) {
        console.error('Error stopping connectivity monitoring:', error);
        throw error;
    }
}

/**
 * Handle browser online event
 */
function handleOnline() {
    console.log('Browser went online');
    
    if (dotNetReference && isMonitoring) {
        try {
            dotNetReference.invokeMethodAsync('OnConnectivityOnline');
        } catch (error) {
            console.error('Error invoking .NET online callback:', error);
        }
    }
}

/**
 * Handle browser offline event
 */
function handleOffline() {
    console.log('Browser went offline');
    
    if (dotNetReference && isMonitoring) {
        try {
            dotNetReference.invokeMethodAsync('OnConnectivityOffline');
        } catch (error) {
            console.error('Error invoking .NET offline callback:', error);
        }
    }
}

/**
 * Get detailed connection information (if available)
 * @returns {object} Connection information object
 */
export function getConnectionInfo() {
    const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
    
    if (!connection) {
        return {
            available: false,
            type: 'unknown',
            effectiveType: 'unknown',
            downlink: null,
            rtt: null
        };
    }

    return {
        available: true,
        type: connection.type || 'unknown',
        effectiveType: connection.effectiveType || 'unknown',
        downlink: connection.downlink || null,
        rtt: connection.rtt || null
    };
}

/**
 * Check if the browser supports connectivity monitoring
 * @returns {boolean} true if supported, false otherwise
 */
export function isSupported() {
    return typeof navigator !== 'undefined' && 
           typeof navigator.onLine !== 'undefined' &&
           typeof window !== 'undefined' &&
           typeof window.addEventListener === 'function';
}

// Export for testing purposes
export const _internal = {
    handleOnline,
    handleOffline,
    isMonitoring: () => isMonitoring,
    getDotNetReference: () => dotNetReference
};