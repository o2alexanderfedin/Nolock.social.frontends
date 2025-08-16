using System.Threading.Tasks;
using NoLock.Social.Core.Camera.Models;
using NoLock.Social.Core.ImageProcessing.Models;

namespace NoLock.Social.Core.ImageProcessing.Interfaces
{
    /// <summary>
    /// Service for enhancing captured images to improve OCR accuracy
    /// </summary>
    public interface IImageEnhancementService
    {
        /// <summary>
        /// Apply full enhancement chain to a captured image
        /// </summary>
        /// <param name="image">The captured image to enhance</param>
        /// <param name="settings">Enhancement settings to use (optional)</param>
        /// <returns>Enhancement result with processed image data</returns>
        Task<EnhancementResult> EnhanceImageAsync(CapturedImage image, EnhancementSettings? settings = null);

        /// <summary>
        /// Auto-adjust image contrast for better text visibility
        /// </summary>
        /// <param name="imageData">Base64 encoded image data</param>
        /// <param name="strength">Contrast adjustment strength (0.1 to 2.0)</param>
        /// <returns>Enhanced image data with adjusted contrast</returns>
        Task<string> AdjustContrastAsync(string imageData, double strength = 1.2);

        /// <summary>
        /// Remove shadows and improve lighting uniformity
        /// </summary>
        /// <param name="imageData">Base64 encoded image data</param>
        /// <param name="intensity">Shadow removal intensity (0.1 to 1.0)</param>
        /// <returns>Enhanced image data with reduced shadows</returns>
        Task<string> RemoveShadowsAsync(string imageData, double intensity = 0.7);

        /// <summary>
        /// Correct perspective distortion for document images
        /// </summary>
        /// <param name="imageData">Base64 encoded image data</param>
        /// <returns>Enhanced image data with corrected perspective</returns>
        Task<string> CorrectPerspectiveAsync(string imageData);

        /// <summary>
        /// Convert image to grayscale for optimal OCR processing
        /// </summary>
        /// <param name="imageData">Base64 encoded image data</param>
        /// <returns>Grayscale image data</returns>
        Task<string> ConvertToGrayscaleAsync(string imageData);

        /// <summary>
        /// Generate a preview showing before and after enhancement comparison
        /// </summary>
        /// <param name="image">The captured image to preview</param>
        /// <param name="settings">Enhancement settings to use (optional)</param>
        /// <returns>Preview with original and enhanced image data</returns>
        Task<EnhancementPreview> GetEnhancementPreviewAsync(CapturedImage image, EnhancementSettings? settings = null);

        /// <summary>
        /// Initialize the image enhancement service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Check if the enhancement service is available and working
        /// </summary>
        /// <returns>True if service is ready, false otherwise</returns>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Clear enhancement cache to free memory
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Get cache statistics for monitoring performance
        /// </summary>
        /// <returns>Tuple containing cache count and average age in minutes</returns>
        (int count, double averageAgeMins) GetCacheStats();
    }
}