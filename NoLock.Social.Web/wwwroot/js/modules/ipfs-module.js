/**
 * IPFS Module - Bridge between Blazor and IPFS MFS operations
 * This module provides the high-level IPFS operations expected by IpfsFileSystemService
 * while delegating to the minimal MFS operations in ipfs-mfs.js
 */

import * as mfs from './ipfs-mfs.js';

// Track write handles for progressive writing
const writeHandles = new Map();
let handleCounter = 0;

/**
 * IPFS namespace object with all operations
 */
export const ipfs = {
    /**
     * Begin a write operation - creates a write handle
     */
    async beginWrite(path) {
        const handleId = `write_${++handleCounter}`;
        writeHandles.set(handleId, {
            path: path,
            buffer: []
        });

        // Return a handle object that can be used to write chunks
        return {
            id: handleId,
            writeChunk: async function(chunk) {
                const handle = writeHandles.get(handleId);
                if (!handle) throw new Error('Write handle not found');

                // Accumulate chunks in buffer
                handle.buffer.push(chunk);
            },
            complete: async function() {
                const handle = writeHandles.get(handleId);
                if (!handle) throw new Error('Write handle not found');

                // Combine all chunks and write to MFS
                const allData = handle.buffer.reduce((acc, chunk) => {
                    const combined = new Uint8Array(acc.length + chunk.length);
                    combined.set(acc);
                    combined.set(chunk, acc.length);
                    return combined;
                }, new Uint8Array(0));

                // Write the complete file
                await mfs.writeBytes(handle.path, allData);

                // Clean up handle
                writeHandles.delete(handleId);

                // Return a mock CID (MFS doesn't return CIDs for writes)
                return `Qm${Math.random().toString(36).substring(2, 15)}`;
            },
            dispose: async function() {
                writeHandles.delete(handleId);
            }
        };
    },

    /**
     * Begin a read operation - creates a read handle
     */
    async beginRead(path) {
        const size = await mfs.getFileSize(path);

        return {
            readChunk: async function(offset, length) {
                if (offset === undefined || offset === null) offset = 0;
                if (length === undefined || length === null) length = 262144; // 256KB default

                return await mfs.readChunk(path, offset, length);
            },
            dispose: async function() {
                // No cleanup needed for reads
            }
        };
    },

    /**
     * List directory contents
     */
    async listDirectory(path) {
        // MFS doesn't have a listDirectory in our minimal implementation
        // Return empty array for now
        console.warn('listDirectory not implemented in minimal MFS wrapper');
        return [];
    },

    /**
     * Check if a file exists
     */
    async exists(path) {
        try {
            const size = await mfs.getFileSize(path);
            return size >= 0;
        } catch {
            return false;
        }
    },

    /**
     * Get file metadata
     */
    async getMetadata(path) {
        try {
            const size = await mfs.getFileSize(path);
            return {
                path: path,
                name: path.split('/').pop(),
                size: size,
                type: 'file',
                cid: `Qm${Math.random().toString(36).substring(2, 15)}`, // Mock CID
                created: null,
                lastModified: null
            };
        } catch {
            return null;
        }
    },

    /**
     * Unpin a file (not applicable in MFS)
     */
    async unpin(path) {
        console.warn('unpin not applicable in MFS context');
        return true;
    }
};

// Export individual functions for direct access
export default ipfs;