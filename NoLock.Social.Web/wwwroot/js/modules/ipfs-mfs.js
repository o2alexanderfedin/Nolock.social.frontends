/**
 * IPFS MFS (Mutable File System) JavaScript wrapper
 * Ultra-thin layer - all logic in C#
 * Only 4 functions as per ipfs-mfs-2.md architecture
 */

/**
 * Write bytes to MFS file (creates or overwrites)
 * @param {string} path - MFS path (e.g., "/videos/video1.mp4")
 * @param {Uint8Array} bytes - Data to write
 * @returns {Promise<void>}
 */
export async function writeBytes(path, bytes) {
    try {
        // Get Helia MFS instance (will be initialized by C#)
        if (!window.heliaMFS) {
            throw new Error("Helia MFS not initialized");
        }

        // Direct MFS write - minimal wrapper
        await window.heliaMFS.writeBytes(path, bytes);
    } catch (error) {
        console.error(`MFS writeBytes failed for ${path}:`, error);
        throw error;
    }
}

/**
 * Append bytes to existing MFS file
 * @param {string} path - MFS path
 * @param {Uint8Array} bytes - Data to append
 * @returns {Promise<void>}
 */
export async function appendBytes(path, bytes) {
    try {
        // Get Helia MFS instance (will be initialized by C#)
        if (!window.heliaMFS) {
            throw new Error("Helia MFS not initialized");
        }

        // Direct MFS append - minimal wrapper
        await window.heliaMFS.appendBytes(path, bytes);
    } catch (error) {
        console.error(`MFS appendBytes failed for ${path}:`, error);
        throw error;
    }
}

/**
 * Read chunk from MFS file
 * @param {string} path - MFS path
 * @param {number} offset - Start position
 * @param {number} length - Bytes to read
 * @returns {Promise<Uint8Array>} - Data chunk
 */
export async function readChunk(path, offset, length) {
    try {
        // Get Helia MFS instance (will be initialized by C#)
        if (!window.heliaMFS) {
            throw new Error("Helia MFS not initialized");
        }

        // Direct MFS read - minimal wrapper, returns Uint8Array
        return await window.heliaMFS.readChunk(path, offset, length);
    } catch (error) {
        console.error(`MFS readChunk failed for ${path}:`, error);
        throw error;
    }
}

/**
 * Get file size from MFS
 * @param {string} path - MFS path
 * @returns {Promise<number>} - File size in bytes
 */
export async function getFileSize(path) {
    try {
        // Get Helia MFS instance (will be initialized by C#)
        if (!window.heliaMFS) {
            throw new Error("Helia MFS not initialized");
        }

        // Get file stats and return size - minimal wrapper
        const stats = await window.heliaMFS.stat(path);
        return stats.size;
    } catch (error) {
        console.error(`MFS getFileSize failed for ${path}:`, error);
        throw error;
    }
}