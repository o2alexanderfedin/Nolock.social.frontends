/**
 * Mock IPFS MFS implementation for testing
 * Simulates IPFS MFS operations in memory for testing memory profiling
 * This follows the ipfs-mfs-2.md specification but uses in-memory storage
 */

// In-memory file system simulation
const mockFileSystem = new Map();

// Initialize mock Helia MFS on window
window.heliaMFS = {
    /**
     * Write bytes to a file in the mock MFS
     * @param {string} path - The file path
     * @param {Uint8Array} bytes - The bytes to write
     */
    async writeBytes(path, bytes) {
        console.log(`Mock MFS: Writing ${bytes.length} bytes to ${path}`);

        // Simulate async operation
        await new Promise(resolve => setTimeout(resolve, 10));

        // Store in memory
        mockFileSystem.set(path, {
            data: bytes,
            timestamp: Date.now(),
            size: bytes.length
        });

        return true;
    },

    /**
     * Append bytes to a file in the mock MFS
     * @param {string} path - The file path
     * @param {Uint8Array} bytes - The bytes to append
     */
    async appendBytes(path, bytes) {
        console.log(`Mock MFS: Appending ${bytes.length} bytes to ${path}`);

        // Simulate async operation
        await new Promise(resolve => setTimeout(resolve, 10));

        const existing = mockFileSystem.get(path);
        if (existing) {
            // Combine existing and new data
            const combined = new Uint8Array(existing.data.length + bytes.length);
            combined.set(existing.data);
            combined.set(bytes, existing.data.length);

            mockFileSystem.set(path, {
                data: combined,
                timestamp: Date.now(),
                size: combined.length
            });
        } else {
            // Create new file
            mockFileSystem.set(path, {
                data: bytes,
                timestamp: Date.now(),
                size: bytes.length
            });
        }

        return true;
    },

    /**
     * Read a chunk of data from a file
     * @param {string} path - The file path
     * @param {number} offset - The offset to start reading from
     * @param {number} length - The number of bytes to read
     * @returns {Uint8Array} The read bytes
     */
    async readChunk(path, offset, length) {
        console.log(`Mock MFS: Reading ${length} bytes from ${path} at offset ${offset}`);

        // Simulate async operation
        await new Promise(resolve => setTimeout(resolve, 10));

        const file = mockFileSystem.get(path);
        if (!file) {
            throw new Error(`File not found: ${path}`);
        }

        // Return the requested chunk
        const end = Math.min(offset + length, file.data.length);
        return file.data.slice(offset, end);
    },

    /**
     * Get the size of a file
     * @param {string} path - The file path
     * @returns {number} The file size in bytes
     */
    async getFileSize(path) {
        console.log(`Mock MFS: Getting size of ${path}`);

        // Simulate async operation
        await new Promise(resolve => setTimeout(resolve, 5));

        const file = mockFileSystem.get(path);
        if (!file) {
            throw new Error(`File not found: ${path}`);
        }

        return file.size;
    },

    /**
     * List all files in the mock file system (for debugging)
     */
    listFiles() {
        const files = [];
        for (const [path, file] of mockFileSystem.entries()) {
            files.push({
                path,
                size: file.size,
                timestamp: file.timestamp
            });
        }
        return files;
    },

    /**
     * Clear all files (for testing)
     */
    clear() {
        mockFileSystem.clear();
        console.log('Mock MFS: Cleared all files');
    },

    /**
     * Get memory usage statistics
     */
    getMemoryStats() {
        let totalBytes = 0;
        let fileCount = 0;

        for (const file of mockFileSystem.values()) {
            totalBytes += file.size;
            fileCount++;
        }

        return {
            totalBytes,
            fileCount,
            filesInMemory: Array.from(mockFileSystem.keys())
        };
    }
};

console.log('Mock IPFS MFS initialized with in-memory storage');
console.log('This is for testing only - real IPFS would use Helia');

export default window.heliaMFS;