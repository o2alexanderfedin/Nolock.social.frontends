// IndexedDB Storage Service for NoLock.Social
// Provides offline storage for document sessions, captured images, and queued operations

window.indexedDbStorage = (function() {
    'use strict';

    const DB_NAME = 'NoLockSocialOfflineStorage';
    const DB_VERSION = 1;
    
    // Object store names
    const STORES = {
        SESSIONS: 'sessions',
        IMAGES: 'images', 
        OPERATIONS: 'operations'
    };

    let db = null;
    let isInitialized = false;

    // Initialize IndexedDB and create object stores
    async function initialize() {
        if (isInitialized && db) {
            return;
        }

        return new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, DB_VERSION);

            request.onerror = () => {
                reject(new Error(`Failed to open IndexedDB: ${request.error}`));
            };

            request.onsuccess = () => {
                db = request.result;
                isInitialized = true;
                console.log('IndexedDB initialized successfully');
                resolve();
            };

            request.onupgradeneeded = (event) => {
                const database = event.target.result;

                // Create sessions object store
                if (!database.objectStoreNames.contains(STORES.SESSIONS)) {
                    const sessionsStore = database.createObjectStore(STORES.SESSIONS, { keyPath: 'id' });
                    sessionsStore.createIndex('sessionId', 'sessionId', { unique: true });
                    sessionsStore.createIndex('createdAt', 'createdAt', { unique: false });
                }

                // Create images object store
                if (!database.objectStoreNames.contains(STORES.IMAGES)) {
                    const imagesStore = database.createObjectStore(STORES.IMAGES, { keyPath: 'id' });
                    imagesStore.createIndex('imageId', 'imageId', { unique: true });
                    imagesStore.createIndex('timestamp', 'timestamp', { unique: false });
                }

                // Create operations object store
                if (!database.objectStoreNames.contains(STORES.OPERATIONS)) {
                    const operationsStore = database.createObjectStore(STORES.OPERATIONS, { keyPath: 'id' });
                    operationsStore.createIndex('operationId', 'operationId', { unique: true });
                    operationsStore.createIndex('priority', 'priority', { unique: false });
                    operationsStore.createIndex('createdAt', 'createdAt', { unique: false });
                }

                console.log('IndexedDB object stores created/updated');
            };
        });
    }

    // Generic helper to perform IndexedDB transactions
    function performTransaction(storeName, mode, operation) {
        return new Promise((resolve, reject) => {
            if (!db) {
                reject(new Error('IndexedDB not initialized'));
                return;
            }

            try {
                const transaction = db.transaction([storeName], mode);
                const store = transaction.objectStore(storeName);

                transaction.onerror = () => {
                    reject(new Error(`Transaction failed: ${transaction.error}`));
                };

                transaction.oncomplete = () => {
                    // Transaction completed successfully
                };

                const request = operation(store);
                
                if (request) {
                    request.onsuccess = () => {
                        resolve(request.result);
                    };
                    
                    request.onerror = () => {
                        reject(new Error(`Operation failed: ${request.error}`));
                    };
                } else {
                    resolve();
                }
            } catch (error) {
                reject(error);
            }
        });
    }

    // Session management functions
    async function saveSession(sessionId, sessionJson) {
        await initialize();
        
        const sessionData = {
            id: sessionId,
            sessionId: sessionId,
            data: sessionJson,
            createdAt: new Date(),
            updatedAt: new Date()
        };

        return performTransaction(STORES.SESSIONS, 'readwrite', (store) => {
            return store.put(sessionData);
        });
    }

    async function loadSession(sessionId) {
        await initialize();
        
        const result = await performTransaction(STORES.SESSIONS, 'readonly', (store) => {
            return store.get(sessionId);
        });

        return result ? result.data : null;
    }

    async function getAllSessions() {
        await initialize();
        
        const results = await performTransaction(STORES.SESSIONS, 'readonly', (store) => {
            return store.getAll();
        });

        return results ? results.map(item => item.data) : [];
    }

    // Image management functions
    async function saveImage(imageId, imageJson) {
        await initialize();
        
        const imageData = {
            id: imageId,
            imageId: imageId,
            data: imageJson,
            timestamp: new Date(),
            size: new Blob([imageJson]).size
        };

        return performTransaction(STORES.IMAGES, 'readwrite', (store) => {
            return store.put(imageData);
        });
    }

    async function loadImage(imageId) {
        await initialize();
        
        const result = await performTransaction(STORES.IMAGES, 'readonly', (store) => {
            return store.get(imageId);
        });

        return result ? result.data : null;
    }

    // Operation queue management functions
    async function queueOperation(operationId, operationJson) {
        await initialize();
        
        const operationData = JSON.parse(operationJson);
        const queueItem = {
            id: operationId,
            operationId: operationId,
            data: operationJson,
            priority: operationData.Priority || 0,
            createdAt: new Date(operationData.CreatedAt || new Date()),
            retryCount: operationData.RetryCount || 0
        };

        return performTransaction(STORES.OPERATIONS, 'readwrite', (store) => {
            return store.put(queueItem);
        });
    }

    async function getPendingOperations() {
        await initialize();
        
        const results = await performTransaction(STORES.OPERATIONS, 'readonly', (store) => {
            return store.getAll();
        });

        if (!results) {
            return [];
        }

        // Sort by priority (lower number = higher priority) then by creation date
        results.sort((a, b) => {
            if (a.priority !== b.priority) {
                return a.priority - b.priority;
            }
            return new Date(a.createdAt) - new Date(b.createdAt);
        });

        return results.map(item => item.data);
    }

    async function removeOperation(operationId) {
        await initialize();
        
        return performTransaction(STORES.OPERATIONS, 'readwrite', (store) => {
            return store.delete(operationId);
        });
    }

    // Cleanup and maintenance functions
    async function clearAllData() {
        await initialize();
        
        const storeNames = [STORES.SESSIONS, STORES.IMAGES, STORES.OPERATIONS];
        
        for (const storeName of storeNames) {
            await performTransaction(storeName, 'readwrite', (store) => {
                return store.clear();
            });
        }
    }

    async function getStorageInfo() {
        await initialize();
        
        const info = {
            sessions: 0,
            images: 0,
            operations: 0,
            totalSize: 0
        };

        // Count items in each store
        for (const [key, storeName] of Object.entries(STORES)) {
            const count = await performTransaction(storeName, 'readonly', (store) => {
                return store.count();
            });
            info[key.toLowerCase()] = count || 0;
        }

        return info;
    }

    function dispose() {
        if (db) {
            db.close();
            db = null;
            isInitialized = false;
            console.log('IndexedDB connection closed');
        }
    }

    // Error handling wrapper
    function handleError(operation, error) {
        console.error(`IndexedDB operation '${operation}' failed:`, error);
        throw error;
    }

    // Public API
    return {
        initialize: () => initialize().catch(error => handleError('initialize', error)),
        
        // Session operations
        saveSession: (sessionId, sessionJson) => 
            saveSession(sessionId, sessionJson).catch(error => handleError('saveSession', error)),
        loadSession: (sessionId) => 
            loadSession(sessionId).catch(error => handleError('loadSession', error)),
        getAllSessions: () => 
            getAllSessions().catch(error => handleError('getAllSessions', error)),
        
        // Image operations
        saveImage: (imageId, imageJson) => 
            saveImage(imageId, imageJson).catch(error => handleError('saveImage', error)),
        loadImage: (imageId) => 
            loadImage(imageId).catch(error => handleError('loadImage', error)),
        
        // Operation queue operations
        queueOperation: (operationId, operationJson) => 
            queueOperation(operationId, operationJson).catch(error => handleError('queueOperation', error)),
        getPendingOperations: () => 
            getPendingOperations().catch(error => handleError('getPendingOperations', error)),
        removeOperation: (operationId) => 
            removeOperation(operationId).catch(error => handleError('removeOperation', error)),
        
        // Maintenance operations
        clearAllData: () => 
            clearAllData().catch(error => handleError('clearAllData', error)),
        getStorageInfo: () => 
            getStorageInfo().catch(error => handleError('getStorageInfo', error)),
        dispose: dispose,
        
        // Health check
        isReady: () => isInitialized && db !== null
    };
})();