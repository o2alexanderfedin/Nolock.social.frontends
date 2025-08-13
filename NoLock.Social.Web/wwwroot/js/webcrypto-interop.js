// Minimal Web Crypto API interop for .NET
// This file only exposes the Web Crypto API functions, all logic is in C#

window.webCryptoInterop = window.webCryptoInterop || {};

// Check if Web Crypto API is available
window.webCryptoInterop.isAvailable = function() {
    return !!(window.crypto && window.crypto.subtle);
};

// Generate random bytes
window.webCryptoInterop.getRandomValues = function(length) {
    const array = new Uint8Array(length);
    window.crypto.getRandomValues(array);
    return array;
};

// SHA-256 hash
window.webCryptoInterop.sha256 = async function(data) {
    const buffer = await window.crypto.subtle.digest('SHA-256', data);
    return new Uint8Array(buffer);
};

// SHA-512 hash
window.webCryptoInterop.sha512 = async function(data) {
    const buffer = await window.crypto.subtle.digest('SHA-512', data);
    return new Uint8Array(buffer);
};

// PBKDF2 key derivation
window.webCryptoInterop.pbkdf2 = async function(password, salt, iterations, keyLength, hash) {
    const keyMaterial = await window.crypto.subtle.importKey(
        'raw',
        password,
        'PBKDF2',
        false,
        ['deriveBits']
    );
    
    const derivedBits = await window.crypto.subtle.deriveBits(
        {
            name: 'PBKDF2',
            salt: salt,
            iterations: iterations,
            hash: hash // 'SHA-256' or 'SHA-512'
        },
        keyMaterial,
        keyLength * 8 // Convert bytes to bits
    );
    
    return new Uint8Array(derivedBits);
};

// Generate ECDSA key pair
window.webCryptoInterop.generateECDSAKeyPair = async function(curve) {
    const keyPair = await window.crypto.subtle.generateKey(
        {
            name: 'ECDSA',
            namedCurve: curve // 'P-256', 'P-384', or 'P-521'
        },
        true, // extractable
        ['sign', 'verify']
    );
    
    // Export both keys in PKCS8/SPKI format
    const publicKey = await window.crypto.subtle.exportKey('spki', keyPair.publicKey);
    const privateKey = await window.crypto.subtle.exportKey('pkcs8', keyPair.privateKey);
    
    return {
        publicKey: new Uint8Array(publicKey),
        privateKey: new Uint8Array(privateKey)
    };
};

// Sign data with ECDSA
window.webCryptoInterop.signECDSA = async function(privateKeyData, data, curve, hash) {
    // Import the private key from PKCS8 format
    const privateKey = await window.crypto.subtle.importKey(
        'pkcs8',
        privateKeyData,
        {
            name: 'ECDSA',
            namedCurve: curve
        },
        false,
        ['sign']
    );
    
    // Sign the data
    const signature = await window.crypto.subtle.sign(
        {
            name: 'ECDSA',
            hash: hash // 'SHA-256' or 'SHA-512'
        },
        privateKey,
        data
    );
    
    return new Uint8Array(signature);
};

// Verify ECDSA signature
window.webCryptoInterop.verifyECDSA = async function(publicKeyData, signature, data, curve, hash) {
    // Import the public key from SPKI format
    const publicKey = await window.crypto.subtle.importKey(
        'spki',
        publicKeyData,
        {
            name: 'ECDSA',
            namedCurve: curve
        },
        false,
        ['verify']
    );
    
    // Verify the signature
    return await window.crypto.subtle.verify(
        {
            name: 'ECDSA',
            hash: hash
        },
        publicKey,
        signature,
        data
    );
};

// AES-GCM encryption
window.webCryptoInterop.encryptAESGCM = async function(key, data, iv) {
    const cryptoKey = await window.crypto.subtle.importKey(
        'raw',
        key,
        'AES-GCM',
        false,
        ['encrypt']
    );
    
    const encrypted = await window.crypto.subtle.encrypt(
        {
            name: 'AES-GCM',
            iv: iv
        },
        cryptoKey,
        data
    );
    
    return new Uint8Array(encrypted);
};

// AES-GCM decryption
window.webCryptoInterop.decryptAESGCM = async function(key, encryptedData, iv) {
    const cryptoKey = await window.crypto.subtle.importKey(
        'raw',
        key,
        'AES-GCM',
        false,
        ['decrypt']
    );
    
    const decrypted = await window.crypto.subtle.decrypt(
        {
            name: 'AES-GCM',
            iv: iv
        },
        cryptoKey,
        encryptedData
    );
    
    return new Uint8Array(decrypted);
};

console.log('Web Crypto interop loaded successfully');