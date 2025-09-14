// Memory Profiler Module for IPFS Testing
// Tracks memory usage, detects leaks, and monitors heap growth

export class MemoryProfiler {
    constructor() {
        this.samples = [];
        this.maxSamples = 100;
        this.baselineMemory = null;
        this.isSupported = this.checkSupport();

        // Mobile memory limits
        this.memoryLimits = {
            iOSSafari: 200 * 1024 * 1024,     // 200MB
            androidGo: 512 * 1024 * 1024,     // 512MB
            androidStandard: 1024 * 1024 * 1024, // 1GB
            desktop: 2048 * 1024 * 1024       // 2GB
        };

        this.currentLimit = this.detectMemoryLimit();
    }

    checkSupport() {
        // Check if performance.memory API is available (Chrome/Edge)
        return typeof performance !== 'undefined' &&
               typeof performance.memory !== 'undefined';
    }

    detectMemoryLimit() {
        const userAgent = navigator.userAgent.toLowerCase();

        if (/iphone|ipad|ipod/.test(userAgent)) {
            return this.memoryLimits.iOSSafari;
        } else if (/android/.test(userAgent)) {
            // Check for low-end Android (Go edition indicators)
            const isLowEnd = navigator.hardwareConcurrency <= 2 ||
                            navigator.deviceMemory <= 1;
            return isLowEnd ? this.memoryLimits.androidGo :
                             this.memoryLimits.androidStandard;
        }

        return this.memoryLimits.desktop;
    }

    captureSnapshot(label = '') {
        if (!this.isSupported) {
            return {
                timestamp: Date.now(),
                label: label,
                supported: false,
                message: 'Memory API not supported in this browser'
            };
        }

        const memory = performance.memory;
        const snapshot = {
            timestamp: Date.now(),
            label: label,
            usedJSHeapSize: memory.usedJSHeapSize,
            totalJSHeapSize: memory.totalJSHeapSize,
            jsHeapSizeLimit: memory.jsHeapSizeLimit,
            // Calculate percentages
            heapUsagePercent: (memory.usedJSHeapSize / memory.totalJSHeapSize) * 100,
            limitUsagePercent: (memory.usedJSHeapSize / this.currentLimit) * 100,
            // Memory pressure indicators
            isHighPressure: memory.usedJSHeapSize > (this.currentLimit * 0.8),
            isCritical: memory.usedJSHeapSize > (this.currentLimit * 0.95)
        };

        // Store sample for leak detection
        this.samples.push(snapshot);
        if (this.samples.length > this.maxSamples) {
            this.samples.shift();
        }

        return snapshot;
    }

    setBaseline() {
        this.baselineMemory = this.captureSnapshot('baseline');
        this.samples = [this.baselineMemory];
        return this.baselineMemory;
    }

    getDelta() {
        if (!this.baselineMemory || !this.isSupported) {
            return null;
        }

        const current = this.captureSnapshot('current');
        return {
            heapGrowth: current.usedJSHeapSize - this.baselineMemory.usedJSHeapSize,
            heapGrowthMB: (current.usedJSHeapSize - this.baselineMemory.usedJSHeapSize) / (1024 * 1024),
            timeElapsed: current.timestamp - this.baselineMemory.timestamp,
            growthRate: this.calculateGrowthRate()
        };
    }

    calculateGrowthRate() {
        if (this.samples.length < 2) return 0;

        const recentSamples = this.samples.slice(-10);
        const firstSample = recentSamples[0];
        const lastSample = recentSamples[recentSamples.length - 1];

        const growth = lastSample.usedJSHeapSize - firstSample.usedJSHeapSize;
        const timeSpan = (lastSample.timestamp - firstSample.timestamp) / 1000; // Convert to seconds

        return timeSpan > 0 ? growth / timeSpan : 0; // Bytes per second
    }

    detectLeaks(threshold = 0.1) {
        if (this.samples.length < 10) {
            return { detected: false, message: 'Insufficient samples for leak detection' };
        }

        // Analyze memory growth pattern
        const growthRates = [];
        for (let i = 1; i < this.samples.length; i++) {
            const growth = this.samples[i].usedJSHeapSize - this.samples[i-1].usedJSHeapSize;
            growthRates.push(growth);
        }

        // Calculate average growth
        const avgGrowth = growthRates.reduce((a, b) => a + b, 0) / growthRates.length;
        const avgGrowthMB = avgGrowth / (1024 * 1024);

        // Detect consistent positive growth (potential leak)
        const positiveGrowthCount = growthRates.filter(g => g > 0).length;
        const leakProbability = positiveGrowthCount / growthRates.length;

        const hasLeak = leakProbability > 0.7 && avgGrowthMB > threshold;

        return {
            detected: hasLeak,
            probability: leakProbability,
            averageGrowthMB: avgGrowthMB,
            message: hasLeak ?
                `Potential memory leak detected: ${avgGrowthMB.toFixed(2)}MB average growth` :
                'No memory leak detected',
            samples: this.samples.length,
            recommendation: hasLeak ?
                'Consider forcing garbage collection or reducing buffer sizes' :
                'Memory usage appears stable'
        };
    }

    getMemoryReport() {
        const current = this.captureSnapshot('report');
        const delta = this.getDelta();
        const leakInfo = this.detectLeaks();

        return {
            current: current,
            baseline: this.baselineMemory,
            delta: delta,
            leakDetection: leakInfo,
            deviceLimit: this.currentLimit,
            deviceLimitMB: this.currentLimit / (1024 * 1024),
            recommendations: this.getRecommendations(current)
        };
    }

    getRecommendations(snapshot) {
        const recommendations = [];

        if (snapshot.isCritical) {
            recommendations.push('CRITICAL: Memory usage above 95% of device limit');
            recommendations.push('Immediately reduce file sizes or clear caches');
        } else if (snapshot.isHighPressure) {
            recommendations.push('WARNING: Memory usage above 80% of device limit');
            recommendations.push('Consider smaller file operations');
        }

        if (snapshot.limitUsagePercent > 50) {
            recommendations.push('Consider using 256KB buffer size for optimal performance');
        }

        const userAgent = navigator.userAgent.toLowerCase();
        if (/iphone|ipad|ipod/.test(userAgent)) {
            recommendations.push('iOS Safari: Limited to ~200MB heap size');
            recommendations.push('Use smaller file chunks to avoid crashes');
        } else if (/android/.test(userAgent) && navigator.deviceMemory <= 1) {
            recommendations.push('Low-end Android device detected');
            recommendations.push('Recommend max 100KB file operations');
        }

        return recommendations;
    }

    formatBytes(bytes) {
        const sizes = ['B', 'KB', 'MB', 'GB'];
        if (bytes === 0) return '0 B';
        const i = Math.floor(Math.log(bytes) / Math.log(1024));
        return (bytes / Math.pow(1024, i)).toFixed(2) + ' ' + sizes[i];
    }

    // Force garbage collection hint (works in some browsers with flags)
    suggestGC() {
        if (typeof window.gc === 'function') {
            window.gc();
            return true;
        }
        return false;
    }
}

// Export singleton instance
export const memoryProfiler = new MemoryProfiler();