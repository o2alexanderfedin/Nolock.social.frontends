namespace NoLock.Social.Core.Camera.Models
{
    /// <summary>
    /// Represents the result of lighting quality assessment
    /// </summary>
    public class LightingQualityResult
    {
        /// <summary>
        /// Lighting quality score from 0-1, where higher values indicate better lighting
        /// </summary>
        public double LightingScore { get; set; }

        /// <summary>
        /// Average brightness level from 0-255
        /// </summary>
        public double Brightness { get; set; }

        /// <summary>
        /// Contrast level from 0-1
        /// </summary>
        public double Contrast { get; set; }

        /// <summary>
        /// Indicates whether lighting is considered adequate
        /// </summary>
        public bool IsAdequate { get; set; }
    }
}