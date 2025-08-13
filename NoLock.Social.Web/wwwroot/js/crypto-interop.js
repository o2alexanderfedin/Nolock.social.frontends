// Cryptographic JavaScript Interop for NoLock.Social
// This file provides browser compatibility checks and crypto API access

window.crypto = window.crypto || {};

// Check if Web Crypto API is available
window.crypto.checkWebCryptoAvailability = function() {
    return !!(window.crypto && window.crypto.subtle);
};

// Check if running in secure context (HTTPS)
window.crypto.checkSecureContext = function() {
    return window.isSecureContext === true;
};

// Get comprehensive browser compatibility information
window.crypto.getBrowserCompatibilityInfo = function() {
    const info = {
        isWebCryptoAvailable: false,
        isSecureContext: false,
        browserName: 'Unknown',
        browserVersion: 'Unknown',
        errorMessage: ''
    };

    // Check Web Crypto API
    info.isWebCryptoAvailable = !!(window.crypto && window.crypto.subtle);
    
    // Check secure context
    info.isSecureContext = window.isSecureContext === true;
    
    // Get browser information
    const userAgent = navigator.userAgent;
    if (userAgent.indexOf('Chrome') > -1) {
        info.browserName = 'Chrome';
        const match = userAgent.match(/Chrome\/([0-9.]+)/);
        if (match) info.browserVersion = match[1];
    } else if (userAgent.indexOf('Firefox') > -1) {
        info.browserName = 'Firefox';
        const match = userAgent.match(/Firefox\/([0-9.]+)/);
        if (match) info.browserVersion = match[1];
    } else if (userAgent.indexOf('Safari') > -1 && userAgent.indexOf('Chrome') === -1) {
        info.browserName = 'Safari';
        const match = userAgent.match(/Version\/([0-9.]+)/);
        if (match) info.browserVersion = match[1];
    } else if (userAgent.indexOf('Edg') > -1) {
        info.browserName = 'Edge';
        const match = userAgent.match(/Edg\/([0-9.]+)/);
        if (match) info.browserVersion = match[1];
    }
    
    // Set error message if not compatible
    if (!info.isWebCryptoAvailable) {
        info.errorMessage = 'Web Crypto API is not supported by this browser. Please use a modern browser (Chrome 60+, Firefox 57+, Safari 11+, or Edge 79+).';
    } else if (!info.isSecureContext) {
        info.errorMessage = 'Application requires HTTPS for cryptographic operations. Please access the site using HTTPS.';
    }
    
    return info;
};

// Web Crypto API wrapper functions
window.crypto.subtle = window.crypto.subtle || {};

// libsodium.js integration
let sodium = null;

// Initialize libsodium
window.crypto.initializeLibsodium = async function() {
    try {
        if (!window.sodium) {
            // Load libsodium if not already loaded
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/libsodium-wrappers@0.7.13/dist/browsers/sodium.js';
            script.async = true;
            
            return new Promise((resolve) => {
                script.onload = async () => {
                    await window.sodium.ready;
                    sodium = window.sodium;
                    console.log('libsodium initialized successfully');
                    resolve(true);
                };
                script.onerror = () => {
                    console.error('Failed to load libsodium');
                    resolve(false);
                };
                document.head.appendChild(script);
            });
        } else {
            await window.sodium.ready;
            sodium = window.sodium;
            return true;
        }
    } catch (error) {
        console.error('Error initializing libsodium:', error);
        return false;
    }
};

// Check if libsodium is ready
window.crypto.isLibsodiumReady = function() {
    return sodium !== null;
};

// Derive key using Argon2id
window.crypto.deriveKeyArgon2id = async function(passphrase, username) {
    if (!sodium) {
        throw new Error('libsodium is not initialized');
    }
    
    // Normalize username to lowercase for salt generation
    const normalizedUsername = username.toLowerCase();
    
    // Generate salt from username using SHA-256
    const salt = await window.crypto.sha256(normalizedUsername);
    
    // Convert passphrase to bytes
    const passphraseBytes = sodium.from_string(passphrase);
    
    // Derive key using Argon2id with exact parameters from requirements
    // CRITICAL: These parameters MUST NOT be changed
    const derivedKey = sodium.crypto_pwhash(
        32, // output length
        passphraseBytes,
        salt.slice(0, 16), // Use first 16 bytes of SHA-256 hash as salt
        3, // iterations (ops limit)
        65536, // memory in KiB (64MB = 65536 KiB) - libsodium uses KiB not bytes
        sodium.crypto_pwhash_ALG_ARGON2ID13
    );
    
    // Clear sensitive data
    sodium.memzero(passphraseBytes);
    
    return derivedKey;
};

// Generate Ed25519 key pair from seed
window.crypto.generateEd25519KeyPairFromSeed = async function(seed) {
    if (!sodium) {
        throw new Error('libsodium is not initialized');
    }
    
    if (!(seed instanceof Uint8Array) || seed.length !== 32) {
        throw new Error('Seed must be a 32-byte Uint8Array');
    }
    
    // Generate key pair from seed
    const keyPair = sodium.crypto_sign_seed_keypair(seed);
    
    return {
        publicKey: keyPair.publicKey,
        privateKey: keyPair.privateKey
    };
};

// Sign data with Ed25519
window.crypto.signEd25519 = async function(data, privateKey) {
    if (!sodium) {
        throw new Error('libsodium is not initialized');
    }
    
    if (!(data instanceof Uint8Array)) {
        throw new Error('Data must be a Uint8Array');
    }
    
    if (!(privateKey instanceof Uint8Array) || privateKey.length !== 64) {
        throw new Error('Private key must be a 64-byte Uint8Array');
    }
    
    // Sign the data
    const signature = sodium.crypto_sign_detached(data, privateKey);
    
    return signature;
};

// Verify Ed25519 signature
window.crypto.verifyEd25519 = async function(data, signature, publicKey) {
    if (!sodium) {
        throw new Error('libsodium is not initialized');
    }
    
    if (!(data instanceof Uint8Array)) {
        throw new Error('Data must be a Uint8Array');
    }
    
    if (!(signature instanceof Uint8Array) || signature.length !== 64) {
        throw new Error('Signature must be a 64-byte Uint8Array');
    }
    
    if (!(publicKey instanceof Uint8Array) || publicKey.length !== 32) {
        throw new Error('Public key must be a 32-byte Uint8Array');
    }
    
    try {
        return sodium.crypto_sign_verify_detached(signature, data, publicKey);
    } catch {
        return false;
    }
};

// SHA-256 hash function using Web Crypto API
window.crypto.sha256 = async function(data) {
    if (!window.crypto.subtle) {
        throw new Error('Web Crypto API is not available');
    }
    
    // Convert string to Uint8Array if needed
    let dataBuffer;
    if (typeof data === 'string') {
        const encoder = new TextEncoder();
        dataBuffer = encoder.encode(data);
    } else if (data instanceof Uint8Array) {
        dataBuffer = data;
    } else {
        throw new Error('Data must be a string or Uint8Array');
    }
    
    // Compute SHA-256 hash
    const hashBuffer = await window.crypto.subtle.digest('SHA-256', dataBuffer);
    return new Uint8Array(hashBuffer);
};

// Secure random bytes generation
window.crypto.getRandomBytes = function(length) {
    if (!window.crypto.getRandomValues) {
        throw new Error('Crypto.getRandomValues is not available');
    }
    
    const bytes = new Uint8Array(length);
    window.crypto.getRandomValues(bytes);
    return bytes;
};

// Base64 encoding/decoding utilities
window.crypto.base64Encode = function(bytes) {
    if (!(bytes instanceof Uint8Array)) {
        throw new Error('Input must be a Uint8Array');
    }
    
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
};

window.crypto.base64Decode = function(base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
};

// Memory clearing utility (best effort - JavaScript doesn't guarantee memory clearing)
window.crypto.clearMemory = function(array) {
    if (array instanceof Uint8Array || array instanceof Array) {
        for (let i = 0; i < array.length; i++) {
            array[i] = 0;
        }
    }
};

console.log('Crypto interop loaded successfully');