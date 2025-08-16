using System;

namespace NoLock.Social.Core.Camera.Models
{
    /// <summary>
    /// Represents a captured image with metadata and assessment information
    /// </summary>
    public class CapturedImage
    {
        /// <summary>
        /// Unique identifier for the captured image
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        /// <summary>
        /// Base64 encoded image data for storage and transmission
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Blob URL for displaying the image in the browser
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the image was captured
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Width of the captured image in pixels
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the captured image in pixels
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Quality assessment score (0-100) indicating image clarity and suitability for OCR
        /// </summary>
        public int Quality { get; set; }
    }
}