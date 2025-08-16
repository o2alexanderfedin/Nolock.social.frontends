using System;
using System.Collections.Generic;

namespace NoLock.Social.Core.ImageProcessing.Models
{
    /// <summary>
    /// Represents the result of an image enhancement operation
    /// </summary>
    public class EnhancementResult
    {
        /// <summary>
        /// Unique identifier for the enhancement operation
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Original image data before enhancement
        /// </summary>
        public string OriginalImageData { get; set; } = string.Empty;

        /// <summary>
        /// Enhanced image data after processing
        /// </summary>
        public string EnhancedImageData { get; set; } = string.Empty;

        /// <summary>
        /// List of enhancement operations that were applied
        /// </summary>
        public List<EnhancementOperation> AppliedOperations { get; set; } = new();

        /// <summary>
        /// Timestamp when the enhancement was completed
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Indicates if the enhancement was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if enhancement failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Quality assessment of the enhanced image (0-100)
        /// </summary>
        public int QualityScore { get; set; }

        /// <summary>
        /// Alias for overall success state
        /// </summary>
        public bool Success => IsSuccessful;

        /// <summary>
        /// Returns an enhanced CapturedImage object
        /// </summary>
        public Core.Camera.Models.CapturedImage EnhancedImage => new Core.Camera.Models.CapturedImage
        {
            ImageData = EnhancedImageData,
            ImageUrl = EnhancedImageData,
            Timestamp = DateTime.UtcNow,
            Quality = QualityScore
        };
    }

    /// <summary>
    /// Configuration settings for image enhancement operations
    /// </summary>
    public class EnhancementSettings
    {
        /// <summary>
        /// Enable automatic contrast adjustment
        /// </summary>
        public bool EnableContrastAdjustment { get; set; } = true;

        /// <summary>
        /// Enable shadow removal
        /// </summary>
        public bool EnableShadowRemoval { get; set; } = true;

        /// <summary>
        /// Enable perspective correction
        /// </summary>
        public bool EnablePerspectiveCorrection { get; set; } = true;

        /// <summary>
        /// Convert to grayscale for better OCR results
        /// </summary>
        public bool ConvertToGrayscale { get; set; } = true;

        /// <summary>
        /// Contrast adjustment strength (0.1 to 2.0)
        /// </summary>
        public double ContrastStrength { get; set; } = 1.2;

        /// <summary>
        /// Shadow removal intensity (0.1 to 1.0)
        /// </summary>
        public double ShadowRemovalIntensity { get; set; } = 0.7;

        /// <summary>
        /// Quality threshold for processing (0-100)
        /// </summary>
        public int QualityThreshold { get; set; } = 50;

        /// <summary>
        /// Maximum processing time in milliseconds
        /// </summary>
        public int MaxProcessingTimeMs { get; set; } = 10000;
    }

    /// <summary>
    /// Represents a single enhancement operation and its result
    /// </summary>
    public class EnhancementOperation
    {
        /// <summary>
        /// Type of enhancement operation
        /// </summary>
        public EnhancementOperationType OperationType { get; set; }

        /// <summary>
        /// Processing time for this operation in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Quality improvement score for this operation (-100 to 100)
        /// </summary>
        public int QualityImprovement { get; set; }

        /// <summary>
        /// Parameters used for this operation
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Types of enhancement operations available
    /// </summary>
    public enum EnhancementOperationType
    {
        ContrastAdjustment,
        ShadowRemoval,
        PerspectiveCorrection,
        GrayscaleConversion,
        NoiseReduction,
        Sharpening
    }

    /// <summary>
    /// Preview result showing before and after comparison
    /// </summary>
    public class EnhancementPreview
    {
        /// <summary>
        /// Original image data
        /// </summary>
        public string OriginalImageData { get; set; } = string.Empty;

        /// <summary>
        /// Preview of enhanced image data
        /// </summary>
        public string PreviewImageData { get; set; } = string.Empty;

        /// <summary>
        /// List of operations that will be applied
        /// </summary>
        public List<EnhancementOperationType> PlannedOperations { get; set; } = new();

        /// <summary>
        /// Estimated processing time in milliseconds
        /// </summary>
        public long EstimatedProcessingTimeMs { get; set; }

        /// <summary>
        /// Predicted quality improvement (0-100)
        /// </summary>
        public int PredictedQualityImprovement { get; set; }
    }

    /// <summary>
    /// Information about image dimensions and size for performance optimization
    /// </summary>
    public class ImageInfo
    {
        /// <summary>
        /// Image width in pixels
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Image height in pixels
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Image file size in megabytes
        /// </summary>
        public double SizeMB { get; set; }

        /// <summary>
        /// Image format (e.g., "jpeg", "png")
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Whether the image is considered large for performance purposes
        /// </summary>
        public bool IsLargeImage => SizeMB > 2.0 || Width > 1920 || Height > 1920;
    }
}
