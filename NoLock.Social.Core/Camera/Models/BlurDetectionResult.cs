namespace NoLock.Social.Core.Camera.Models
{
    /// <summary>
    /// Represents the result of blur detection analysis
    /// </summary>
    public class BlurDetectionResult
    {
        /// <summary>
        /// Blur score from 0-1, where higher values indicate sharper images
        /// </summary>
        public double BlurScore { get; set; }

        /// <summary>
        /// Indicates whether the image is considered blurry
        /// </summary>
        public bool IsBlurry { get; set; }

        /// <summary>
        /// Threshold used for blur detection
        /// </summary>
        public double BlurThreshold { get; set; }
    }
}