namespace NoLock.Social.Core.Camera.Models
{
    /// <summary>
    /// Represents the result of edge detection analysis for documents
    /// </summary>
    public class EdgeDetectionResult
    {
        /// <summary>
        /// Edge detection score from 0-1, where higher values indicate better edge detection
        /// </summary>
        public double EdgeScore { get; set; }

        /// <summary>
        /// Number of edges detected in the image
        /// </summary>
        public int EdgeCount { get; set; }

        /// <summary>
        /// Indicates whether document edges were clearly detected
        /// </summary>
        public bool HasClearEdges { get; set; }

        /// <summary>
        /// Confidence level in edge detection from 0-1
        /// </summary>
        public double Confidence { get; set; }
    }
}