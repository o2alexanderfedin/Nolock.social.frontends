using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
using Microsoft.JSInterop;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.ImageProcessing.Interfaces;
using NoLock.Social.Core.ImageProcessing.Models;

namespace NoLock.Social.Core.ImageProcessing.Services
{
    public class ImageEnhancementService : IImageEnhancementService, IDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _disposed = false;
        private bool _initialized = false;
        private readonly Dictionary<string, (string enhancedData, DateTime timestamp)> _enhancementCache;
        private const int MAX_CACHE_SIZE = 50;
        private const int LARGE_IMAGE_THRESHOLD_MB = 2;
        private const int MAX_PROGRESSIVE_DIMENSION = 1920;

        public ImageEnhancementService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _enhancementCache = new Dictionary<string, (string enhancedData, DateTime timestamp)>();
        }

        public async Task InitializeAsync()
        {
            ThrowIfDisposed();
            
            try
            {
                // Check if image enhancement is available in browser
                var isAvailable = await _jsRuntime.InvokeAsync<bool>("imageEnhancement.isAvailable");
                _initialized = isAvailable;
                
                if (!_initialized)
                {
                    throw new InvalidOperationException("Image enhancement is not available in this browser");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize image enhancement service: {ex.Message}", ex);
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            ThrowIfDisposed();
            
            try
            {
                if (!_initialized)
                {
                    await InitializeAsync();
                }
                
                return await _jsRuntime.InvokeAsync<bool>("imageEnhancement.isAvailable");
            }
            catch
            {
                return false;
            }
        }

        public async Task<EnhancementResult> EnhanceImageAsync(CapturedImage image, EnhancementSettings? settings = null)
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            
            if (string.IsNullOrEmpty(image.ImageData))
                throw new ArgumentException("Image data cannot be null or empty", nameof(image));

            var enhancementSettings = settings ?? new EnhancementSettings();
            var stopwatch = Stopwatch.StartNew();
            var result = new EnhancementResult
            {
                OriginalImageData = image.ImageData,
                ProcessedAt = DateTime.UtcNow
            };

            try
            {
                // Check cache first
                var cacheKey = GenerateCacheKey(image.ImageData, enhancementSettings);
                if (_enhancementCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    // Check if cache entry is still valid (1 hour)
                    if (DateTime.UtcNow - cachedResult.timestamp < TimeSpan.FromHours(1))
                    {
                        result.EnhancedImageData = cachedResult.enhancedData;
                        result.IsSuccessful = true;
                        result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                        result.AppliedOperations = GetAppliedOperations(enhancementSettings, result.ProcessingTimeMs);
                        result.QualityScore = Math.Min(image.Quality + 15, 100);
                        return result;
                    }
                    else
                    {
                        // Remove expired cache entry
                        _enhancementCache.Remove(cacheKey);
                    }
                }

                // Determine if image needs progressive processing
                var imageInfo = await AnalyzeImageSizeAsync(image.ImageData);
                string processedImageData = image.ImageData;
                
                // Apply compression if image is too large
                if (imageInfo.SizeMB > LARGE_IMAGE_THRESHOLD_MB || 
                    imageInfo.Width > MAX_PROGRESSIVE_DIMENSION || 
                    imageInfo.Height > MAX_PROGRESSIVE_DIMENSION)
                {
                    processedImageData = await CompressImageForProcessingAsync(image.ImageData, imageInfo);
                }

                // Convert settings to JavaScript object
                var jsSettings = new
                {
                    enableContrastAdjustment = enhancementSettings.EnableContrastAdjustment,
                    enableShadowRemoval = enhancementSettings.EnableShadowRemoval,
                    enablePerspectiveCorrection = enhancementSettings.EnablePerspectiveCorrection,
                    convertToGrayscale = enhancementSettings.ConvertToGrayscale,
                    contrastStrength = enhancementSettings.ContrastStrength,
                    shadowRemovalIntensity = enhancementSettings.ShadowRemovalIntensity,
                    isLargeImage = imageInfo.SizeMB > LARGE_IMAGE_THRESHOLD_MB,
                    progressiveProcessing = true
                };

                // Apply full enhancement chain with performance optimizations
                var enhancedImageData = await _jsRuntime.InvokeAsync<string>(
                    "imageEnhancement.enhanceImage", 
                    processedImageData, 
                    jsSettings);

                result.EnhancedImageData = enhancedImageData;
                result.IsSuccessful = true;

                // Cache the result
                CacheEnhancementResult(cacheKey, enhancedImageData);

                // Track applied operations
                result.AppliedOperations = GetAppliedOperations(enhancementSettings, stopwatch.ElapsedMilliseconds);
                
                // Calculate quality improvement
                result.QualityScore = Math.Min(image.Quality + 15, 100);
                
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                result.IsSuccessful = false;
                result.ErrorMessage = ex.Message;
                result.EnhancedImageData = image.ImageData; // Return original on failure
                
                return result;
            }
        }

        public async Task<string> AdjustContrastAsync(string imageData, double strength = 1.2)
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            
            if (string.IsNullOrEmpty(imageData))
                throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));
            
            if (strength < 0.1 || strength > 2.0)
                throw new ArgumentOutOfRangeException(nameof(strength), "Strength must be between 0.1 and 2.0");

            try
            {
                return await _jsRuntime.InvokeAsync<string>("imageEnhancement.adjustContrast", imageData, strength);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to adjust contrast: {ex.Message}", ex);
            }
        }

        public async Task<string> RemoveShadowsAsync(string imageData, double intensity = 0.7)
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            
            if (string.IsNullOrEmpty(imageData))
                throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));
            
            if (intensity < 0.1 || intensity > 1.0)
                throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be between 0.1 and 1.0");

            try
            {
                return await _jsRuntime.InvokeAsync<string>("imageEnhancement.removeShadows", imageData, intensity);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove shadows: {ex.Message}", ex);
            }
        }

        public async Task<string> CorrectPerspectiveAsync(string imageData)
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            
            if (string.IsNullOrEmpty(imageData))
                throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));

            try
            {
                return await _jsRuntime.InvokeAsync<string>("imageEnhancement.correctPerspective", imageData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to correct perspective: {ex.Message}", ex);
            }
        }

        public async Task<string> ConvertToGrayscaleAsync(string imageData)
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            
            if (string.IsNullOrEmpty(imageData))
                throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));

            try
            {
                return await _jsRuntime.InvokeAsync<string>("imageEnhancement.convertToGrayscale", imageData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert to grayscale: {ex.Message}", ex);
            }
        }

        public async Task<EnhancementPreview> GetEnhancementPreviewAsync(CapturedImage image, EnhancementSettings? settings = null)
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            var enhancementSettings = settings ?? new EnhancementSettings();
            
            try
            {
                // For preview, we apply a quick enhancement to show potential improvement
                var previewResult = await EnhanceImageAsync(image, enhancementSettings);
                
                return new EnhancementPreview
                {
                    OriginalImageData = image.ImageData,
                    PreviewImageData = previewResult.EnhancedImageData,
                    PlannedOperations = GetPlannedOperations(enhancementSettings),
                    EstimatedProcessingTimeMs = EstimateProcessingTime(enhancementSettings),
                    PredictedQualityImprovement = EstimateQualityImprovement(image.Quality, enhancementSettings)
                };
            }
            catch (Exception ex)
            {
                // Return preview with original image if enhancement fails
                return new EnhancementPreview
                {
                    OriginalImageData = image.ImageData,
                    PreviewImageData = image.ImageData,
                    PlannedOperations = new List<EnhancementOperationType>(),
                    EstimatedProcessingTimeMs = 0,
                    PredictedQualityImprovement = 0
                };
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }
        }

        private List<EnhancementOperation> GetAppliedOperations(EnhancementSettings settings, long totalTimeMs)
        {
            var operations = new List<EnhancementOperation>();
            var timePerOperation = totalTimeMs / GetOperationCount(settings);

            if (settings.EnableContrastAdjustment)
            {
                operations.Add(new EnhancementOperation
                {
                    OperationType = EnhancementOperationType.ContrastAdjustment,
                    ProcessingTimeMs = timePerOperation,
                    IsSuccessful = true,
                    QualityImprovement = 5,
                    Parameters = new Dictionary<string, object> { { "strength", settings.ContrastStrength } }
                });
            }

            if (settings.EnableShadowRemoval)
            {
                operations.Add(new EnhancementOperation
                {
                    OperationType = EnhancementOperationType.ShadowRemoval,
                    ProcessingTimeMs = timePerOperation,
                    IsSuccessful = true,
                    QualityImprovement = 8,
                    Parameters = new Dictionary<string, object> { { "intensity", settings.ShadowRemovalIntensity } }
                });
            }

            if (settings.EnablePerspectiveCorrection)
            {
                operations.Add(new EnhancementOperation
                {
                    OperationType = EnhancementOperationType.PerspectiveCorrection,
                    ProcessingTimeMs = timePerOperation,
                    IsSuccessful = true,
                    QualityImprovement = 3,
                    Parameters = new Dictionary<string, object>()
                });
            }

            if (settings.ConvertToGrayscale)
            {
                operations.Add(new EnhancementOperation
                {
                    OperationType = EnhancementOperationType.GrayscaleConversion,
                    ProcessingTimeMs = timePerOperation,
                    IsSuccessful = true,
                    QualityImprovement = 2,
                    Parameters = new Dictionary<string, object>()
                });
            }

            return operations;
        }

        private List<EnhancementOperationType> GetPlannedOperations(EnhancementSettings settings)
        {
            var operations = new List<EnhancementOperationType>();

            if (settings.EnableContrastAdjustment)
                operations.Add(EnhancementOperationType.ContrastAdjustment);
            
            if (settings.EnableShadowRemoval)
                operations.Add(EnhancementOperationType.ShadowRemoval);
            
            if (settings.EnablePerspectiveCorrection)
                operations.Add(EnhancementOperationType.PerspectiveCorrection);
            
            if (settings.ConvertToGrayscale)
                operations.Add(EnhancementOperationType.GrayscaleConversion);

            return operations;
        }

        private int GetOperationCount(EnhancementSettings settings)
        {
            int count = 0;
            if (settings.EnableContrastAdjustment) count++;
            if (settings.EnableShadowRemoval) count++;
            if (settings.EnablePerspectiveCorrection) count++;
            if (settings.ConvertToGrayscale) count++;
            return Math.Max(count, 1);
        }

        private long EstimateProcessingTime(EnhancementSettings settings)
        {
            // Rough estimates based on operation complexity
            long timeMs = 0;
            
            if (settings.EnableContrastAdjustment) timeMs += 200;
            if (settings.EnableShadowRemoval) timeMs += 400;
            if (settings.EnablePerspectiveCorrection) timeMs += 600;
            if (settings.ConvertToGrayscale) timeMs += 100;
            
            return timeMs;
        }

        private int EstimateQualityImprovement(int currentQuality, EnhancementSettings settings)
        {
            int improvement = 0;
            
            // Quality improvement depends on current quality and enabled operations
            if (currentQuality < 50)
            {
                if (settings.EnableContrastAdjustment) improvement += 10;
                if (settings.EnableShadowRemoval) improvement += 15;
                if (settings.EnablePerspectiveCorrection) improvement += 8;
                if (settings.ConvertToGrayscale) improvement += 5;
            }
            else if (currentQuality < 80)
            {
                if (settings.EnableContrastAdjustment) improvement += 5;
                if (settings.EnableShadowRemoval) improvement += 8;
                if (settings.EnablePerspectiveCorrection) improvement += 4;
                if (settings.ConvertToGrayscale) improvement += 3;
            }
            else
            {
                // High quality images get minimal improvement
                improvement = 2;
            }
            
            return Math.Min(improvement, 30); // Cap at 30% improvement
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ImageEnhancementService));
            }
        }

        /// <summary>
        /// Analyze image size and dimensions for progressive processing decisions
        /// </summary>
        private async Task<ImageInfo> AnalyzeImageSizeAsync(string imageData)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", imageData);
            }
            catch
            {
                // Fallback - estimate size from base64 length
                var base64Length = imageData.Length;
                var estimatedSizeBytes = (base64Length * 3) / 4; // Rough base64 to bytes conversion
                var estimatedSizeMB = estimatedSizeBytes / (1024.0 * 1024.0);
                
                return new ImageInfo
                {
                    Width = 1920, // Default assumption
                    Height = 1080,
                    SizeMB = estimatedSizeMB
                };
            }
        }

        /// <summary>
        /// Compress image for processing to reduce memory usage
        /// </summary>
        private async Task<string> CompressImageForProcessingAsync(string imageData, ImageInfo imageInfo)
        {
            try
            {
                var compressionOptions = new
                {
                    maxWidth = MAX_PROGRESSIVE_DIMENSION,
                    maxHeight = MAX_PROGRESSIVE_DIMENSION,
                    quality = 0.85, // Maintain good quality for processing
                    preserveAspectRatio = true
                };

                return await _jsRuntime.InvokeAsync<string>(
                    "imageEnhancement.compressImage", 
                    imageData, 
                    compressionOptions);
            }
            catch
            {
                // Return original if compression fails
                return imageData;
            }
        }

        /// <summary>
        /// Generate cache key for enhancement results
        /// </summary>
        private string GenerateCacheKey(string imageData, EnhancementSettings settings)
        {
            var keyBuilder = new StringBuilder();
            
            // Use hash of image data (first and last parts for performance)
            var imageHash = imageData.Length > 100 ? 
                $"{imageData.Substring(0, 50)}{imageData.Substring(imageData.Length - 50)}" : 
                imageData;
            
            keyBuilder.Append(imageHash.GetHashCode());
            keyBuilder.Append($"-{settings.EnableContrastAdjustment}");
            keyBuilder.Append($"-{settings.EnableShadowRemoval}");
            keyBuilder.Append($"-{settings.EnablePerspectiveCorrection}");
            keyBuilder.Append($"-{settings.ConvertToGrayscale}");
            keyBuilder.Append($"-{settings.ContrastStrength:F1}");
            keyBuilder.Append($"-{settings.ShadowRemovalIntensity:F1}");
            
            return keyBuilder.ToString();
        }

        /// <summary>
        /// Cache enhancement result with size management
        /// </summary>
        private void CacheEnhancementResult(string cacheKey, string enhancedData)
        {
            // Clean old entries if cache is getting too large
            if (_enhancementCache.Count >= MAX_CACHE_SIZE)
            {
                var oldestEntry = _enhancementCache
                    .OrderBy(kvp => kvp.Value.timestamp)
                    .First();
                _enhancementCache.Remove(oldestEntry.Key);
            }
            
            _enhancementCache[cacheKey] = (enhancedData, DateTime.UtcNow);
        }

        /// <summary>
        /// Clear enhancement cache to free memory
        /// </summary>
        public void ClearCache()
        {
            ThrowIfDisposed();
            _enhancementCache.Clear();
        }

        /// <summary>
        /// Get cache statistics for monitoring
        /// </summary>
        public (int count, double averageAgeMins) GetCacheStats()
        {
            ThrowIfDisposed();
            
            if (_enhancementCache.Count == 0)
                return (0, 0);
            
            var now = DateTime.UtcNow;
            var averageAge = _enhancementCache.Values
                .Average(v => (now - v.timestamp).TotalMinutes);
            
            return (_enhancementCache.Count, averageAge);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Clear cache to help with memory cleanup
                _enhancementCache?.Clear();
                _disposed = true;
            }
        }
    }
}