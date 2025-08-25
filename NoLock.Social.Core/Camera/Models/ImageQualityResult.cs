namespace NoLock.Social.Core.Camera.Models
{
    /// <summary>
    /// Represents the result of image quality analysis for camera captures
    /// </summary>
    public class ImageQualityResult
    {
        /// <summary>
        /// Overall quality score from 0-100, where 100 is perfect quality
        /// </summary>
        public int OverallScore { get; set; }

        /// <summary>
        /// Blur detection score from 0-1, where higher values indicate sharper images
        /// </summary>
        public double BlurScore { get; set; }

        /// <summary>
        /// Lighting quality score from 0-1, where higher values indicate better lighting conditions
        /// </summary>
        public double LightingScore { get; set; }

        /// <summary>
        /// Edge detection score from 0-1, where higher values indicate better document edge detection
        /// </summary>
        public double EdgeDetectionScore { get; set; }

        /// <summary>
        /// Indicates whether the image has quality issues that should be addressed
        /// </summary>
        public bool HasIssues => Issues.Any();

        /// <summary>
        /// List of specific quality issues detected in the image
        /// </summary>
        public List<string> Issues { get; set; } = new List<string>();

        /// <summary>
        /// List of suggestions for improving image quality
        /// </summary>
        public List<string> Suggestions { get; set; } = new List<string>();

        /// <summary>
        /// Indicates if the quality is acceptable for processing
        /// </summary>
        public bool IsAcceptable => OverallScore >= 70;
    }
}
