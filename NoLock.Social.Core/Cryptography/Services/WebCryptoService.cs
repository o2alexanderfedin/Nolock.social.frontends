using Microsoft.JSInterop;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Cryptography.Services
{
    /// <summary>
    /// Service for Web Crypto API interop
    /// </summary>
    public class WebCryptoService : IWebCryptoService
    {
        private readonly IJSRuntime _jsRuntime;

        public WebCryptoService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("webCryptoInterop.isAvailable");
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> GetRandomBytesAsync(int length)
        {
            return await _jsRuntime.InvokeAsync<byte[]>("webCryptoInterop.getRandomValues", length);
        }

        public async Task<byte[]> Sha256Async(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return await _jsRuntime.InvokeAsync<byte[]>("webCryptoInterop.sha256", data);
        }

        public async Task<byte[]> Sha512Async(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return await _jsRuntime.InvokeAsync<byte[]>("webCryptoInterop.sha512", data);
        }

        public async Task<byte[]> Pbkdf2Async(byte[] password, byte[] salt, int iterations, int keyLength, string hash = "SHA-256")
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            if (salt == null)
                throw new ArgumentNullException(nameof(salt));
            if (iterations <= 0)
                throw new ArgumentException("Iterations must be positive", nameof(iterations));
            if (keyLength <= 0)
                throw new ArgumentException("Key length must be positive", nameof(keyLength));

            return await _jsRuntime.InvokeAsync<byte[]>("webCryptoInterop.pbkdf2", 
                password, salt, iterations, keyLength, hash);
        }

        public async Task<ECDSAKeyPair> GenerateECDSAKeyPairAsync(string curve = "P-256")
        {
            var result = await _jsRuntime.InvokeAsync<ECDSAKeyPair>("webCryptoInterop.generateECDSAKeyPair", curve);
            return result ?? new ECDSAKeyPair();
        }

        public async Task<byte[]> SignECDSAAsync(byte[] privateKey, byte[] data, string curve = "P-256", string hash = "SHA-256")
        {
            if (privateKey == null)
                throw new ArgumentNullException(nameof(privateKey));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return await _jsRuntime.InvokeAsync<byte[]>("webCryptoInterop.signECDSA", 
                privateKey, data, curve, hash);
        }

        public async Task<bool> VerifyECDSAAsync(byte[] publicKey, byte[] signature, byte[] data, string curve = "P-256", string hash = "SHA-256")
        {
            if (publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return await _jsRuntime.InvokeAsync<bool>("webCryptoInterop.verifyECDSA", 
                publicKey, signature, data, curve, hash);
        }

        public async Task<byte[]> EncryptAESGCMAsync(byte[] key, byte[] data, byte[] iv)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (iv == null)
                throw new ArgumentNullException(nameof(iv));

            return await _jsRuntime.InvokeAsync<byte[]>("webCryptoInterop.encryptAESGCM", key, data, iv);
        }

        public async Task<byte[]> DecryptAESGCMAsync(byte[] key, byte[] encryptedData, byte[] iv)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (encryptedData == null)
                throw new ArgumentNullException(nameof(encryptedData));
            if (iv == null)
                throw new ArgumentNullException(nameof(iv));

            return await _jsRuntime.InvokeAsync<byte[]>("webCryptoInterop.decryptAESGCM", key, encryptedData, iv);
        }
    }
}