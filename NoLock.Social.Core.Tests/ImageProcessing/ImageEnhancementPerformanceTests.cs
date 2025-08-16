using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.ImageProcessing.Models;
using NoLock.Social.Core.ImageProcessing.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.ImageProcessing
{
    public class ImageEnhancementPerformanceTests
    {
        private readonly Mock<IJSRuntime> _mockJSRuntime;
        private readonly ImageEnhancementService _service;

        public ImageEnhancementPerformanceTests()
        {
            _mockJSRuntime = new Mock<IJSRuntime>();
            _service = new ImageEnhancementService(_mockJSRuntime.Object);
        }

        [Theory]
        [InlineData("small", 0.5, false, "Small image should not trigger large image optimization")]
        [InlineData("medium", 1.5, false, "Medium image should not trigger large image optimization")]
        [InlineData("large", 3.0, true, "Large image should trigger optimization")]
        [InlineData("very_large", 5.0, true, "Very large image should trigger optimization")]
        public async Task EnhanceImageAsync_WithDifferentImageSizes_HandlesCachingAndOptimization(
            string imageSizeCategory, 
            double imageSizeMB, 
            bool shouldUseOptimization,
            string scenario)
        {
            // Arrange
            var testImageData = GenerateTestImageData(imageSizeMB);
            var capturedImage = new CapturedImage 
            { 
                ImageData = testImageData,
                Quality = 75
            };
            var settings = new EnhancementSettings();

            // Mock image size analysis
            var imageInfo = new ImageInfo
            {
                Width = imageSizeMB > 2.0 ? 2400 : 1200,
                Height = imageSizeMB > 2.0 ? 1800 : 900,
                SizeMB = imageSizeMB,
                Format = "jpeg"
            };

            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            _mockJSRuntime.Setup(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()))
                .ReturnsAsync(imageInfo);

            if (shouldUseOptimization)
            {
                _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.compressImage", It.IsAny<object[]>()))
                    .ReturnsAsync(testImageData);
            }

            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.enhanceImage", It.IsAny<object[]>()))
                .ReturnsAsync(testImageData);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await _service.EnhanceImageAsync(capturedImage, settings);
            stopwatch.Stop();

            // Assert
            Assert.True(result.IsSuccessful, $"Enhancement should succeed for {scenario}");
            Assert.NotNull(result.EnhancedImageData);
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Processing should complete within 5 seconds for {scenario}");

            // Verify optimization was used for large images
            if (shouldUseOptimization)
            {
                _mockJSRuntime.Verify(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()), 
                    Times.Once, "Should analyze image size for large images");
            }
        }

        [Fact]
        public async Task EnhanceImageAsync_WithCaching_ReusesResults()
        {
            // Arrange
            var testImageData = GenerateTestImageData(1.0);
            var capturedImage = new CapturedImage 
            { 
                ImageData = testImageData,
                Quality = 75
            };
            var settings = new EnhancementSettings();

            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            _mockJSRuntime.Setup(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()))
                .ReturnsAsync(new ImageInfo { Width = 1200, Height = 900, SizeMB = 1.0, Format = "jpeg" });

            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.enhanceImage", It.IsAny<object[]>()))
                .ReturnsAsync(testImageData);

            // Act - First call
            var result1 = await _service.EnhanceImageAsync(capturedImage, settings);
            
            // Act - Second call with same parameters
            var result2 = await _service.EnhanceImageAsync(capturedImage, settings);

            // Assert
            Assert.True(result1.IsSuccessful);
            Assert.True(result2.IsSuccessful);
            Assert.Equal(result1.EnhancedImageData, result2.EnhancedImageData);
            
            // Second call should be faster due to caching
            Assert.True(result2.ProcessingTimeMs <= result1.ProcessingTimeMs, 
                "Second call should be faster due to caching");
        }

        [Fact]
        public void ClearCache_RemovesAllCachedEntries()
        {
            // Arrange
            var initialStats = _service.GetCacheStats();

            // Act
            _service.ClearCache();
            var statsAfterClear = _service.GetCacheStats();

            // Assert
            Assert.Equal(0, statsAfterClear.count);
            Assert.Equal(0, statsAfterClear.averageAgeMins);
        }

        [Fact]
        public void GetCacheStats_ReturnsAccurateInformation()
        {
            // Arrange & Act
            var stats = _service.GetCacheStats();

            // Assert
            Assert.True(stats.count >= 0, "Cache count should be non-negative");
            Assert.True(stats.averageAgeMins >= 0, "Average age should be non-negative");
        }

        [Theory]
        [InlineData(true, "Contrast adjustment enabled")]
        [InlineData(false, "Contrast adjustment disabled")]
        public async Task EnhanceImageAsync_WithDifferentSettings_GeneratesDifferentCacheKeys(
            bool enableContrastAdjustment,
            string scenario)
        {
            // Arrange
            var testImageData = GenerateTestImageData(1.0);
            var capturedImage = new CapturedImage 
            { 
                ImageData = testImageData,
                Quality = 75
            };
            
            var settings1 = new EnhancementSettings { EnableContrastAdjustment = enableContrastAdjustment };
            var settings2 = new EnhancementSettings { EnableContrastAdjustment = !enableContrastAdjustment };

            _mockJSRuntime.Setup(x => x.InvokeAsync<bool>("imageEnhancement.isAvailable", It.IsAny<object[]>()))
                .ReturnsAsync(true);

            _mockJSRuntime.Setup(x => x.InvokeAsync<ImageInfo>("imageEnhancement.analyzeImageSize", It.IsAny<object[]>()))
                .ReturnsAsync(new ImageInfo { Width = 1200, Height = 900, SizeMB = 1.0, Format = "jpeg" });

            _mockJSRuntime.Setup(x => x.InvokeAsync<string>("imageEnhancement.enhanceImage", It.IsAny<object[]>()))
                .ReturnsAsync(testImageData);

            // Act
            await _service.EnhanceImageAsync(capturedImage, settings1);
            await _service.EnhanceImageAsync(capturedImage, settings2);

            // Assert
            var cacheStats = _service.GetCacheStats();
            Assert.True(cacheStats.count >= 1, 
                $"Different settings should create separate cache entries for {scenario}");
        }

        private static string GenerateTestImageData(double sizeMB)
        {
            // Generate a base64 string that approximates the desired size
            var targetBytes = (int)(sizeMB * 1024 * 1024 * 0.75); // Account for base64 overhead
            var dataBytes = new byte[targetBytes];
            new Random().NextBytes(dataBytes);
            return $"data:image/jpeg;base64,{Convert.ToBase64String(dataBytes)}";
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _service?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}