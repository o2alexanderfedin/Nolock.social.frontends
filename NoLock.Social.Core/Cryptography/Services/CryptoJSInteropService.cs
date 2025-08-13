using Microsoft.JSInterop;
using NoLock.Social.Core.Cryptography.Interfaces;
using System;
using System.Text;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for JavaScript interop with Web Crypto API and libsodium.js
    /// </summary>
    public class CryptoJSInteropService : ICryptoJSInteropService
    {
        private readonly IJSRuntime _jsRuntime;

        public CryptoJSInteropService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public async Task<bool> InitializeLibsodiumAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("crypto.initializeLibsodium");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsLibsodiumReadyAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("crypto.isLibsodiumReady");
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> ComputeSha256Async(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return await _jsRuntime.InvokeAsync<byte[]>("crypto.sha256", data);
        }

        public async Task<byte[]> ComputeSha256Async(string data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return await _jsRuntime.InvokeAsync<byte[]>("crypto.sha256", data);
        }

        public async Task<byte[]> GetRandomBytesAsync(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0");

            return await _jsRuntime.InvokeAsync<byte[]>("crypto.getRandomBytes", length);
        }

        public async Task<byte[]> DeriveKeyArgon2idAsync(string passphrase, string username)
        {
            if (string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("Passphrase cannot be null or empty", nameof(passphrase));
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            return await _jsRuntime.InvokeAsync<byte[]>("crypto.deriveKeyArgon2id", passphrase, username);
        }

        public async Task<Ed25519KeyPair> GenerateEd25519KeyPairFromSeedAsync(byte[] seed)
        {
            if (seed == null || seed.Length != 32)
                throw new ArgumentException("Seed must be exactly 32 bytes", nameof(seed));

            return await _jsRuntime.InvokeAsync<Ed25519KeyPair>("crypto.generateEd25519KeyPairFromSeed", seed);
        }

        public async Task<byte[]> SignEd25519Async(byte[] data, byte[] privateKey)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (privateKey == null || privateKey.Length != 64)
                throw new ArgumentException("Private key must be exactly 64 bytes", nameof(privateKey));

            return await _jsRuntime.InvokeAsync<byte[]>("crypto.signEd25519", data, privateKey);
        }

        public async Task<bool> VerifyEd25519Async(byte[] data, byte[] signature, byte[] publicKey)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (signature == null || signature.Length != 64)
                throw new ArgumentException("Signature must be exactly 64 bytes", nameof(signature));
            if (publicKey == null || publicKey.Length != 32)
                throw new ArgumentException("Public key must be exactly 32 bytes", nameof(publicKey));

            return await _jsRuntime.InvokeAsync<bool>("crypto.verifyEd25519", data, signature, publicKey);
        }

        public async Task<string> BytesToBase64Async(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            return await _jsRuntime.InvokeAsync<string>("crypto.base64Encode", bytes);
        }

        public async Task<byte[]> Base64ToBytesAsync(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                throw new ArgumentException("Base64 string cannot be null or empty", nameof(base64));

            return await _jsRuntime.InvokeAsync<byte[]>("crypto.base64Decode", base64);
        }

        public async Task ClearMemoryAsync(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            // Clear in C# first
            Array.Clear(data, 0, data.Length);

            // Also attempt to clear in JavaScript (best effort)
            try
            {
                await _jsRuntime.InvokeAsync<object>("crypto.clearMemory", (object)data);
            }
            catch
            {
                // Best effort - ignore failures
            }
        }
    }
}