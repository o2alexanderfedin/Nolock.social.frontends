namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents the confidence level of an OCR extraction result.
    /// </summary>
    public enum ConfidenceLevel
    {
        /// <summary>
        /// Low confidence (0.0 - 0.59).
        /// </summary>
        Low,

        /// <summary>
        /// Medium confidence (0.60 - 0.79).
        /// </summary>
        Medium,

        /// <summary>
        /// High confidence (0.80 - 1.0).
        /// </summary>
        High
    }

    /// <summary>
    /// Helper methods for working with confidence scores and levels.
    /// </summary>
    public static class ConfidenceHelper
    {
        /// <summary>
        /// Converts a confidence score to a confidence level.
        /// </summary>
        /// <param name="score">The confidence score (0.0 to 1.0).</param>
        /// <returns>The corresponding confidence level.</returns>
        public static ConfidenceLevel GetConfidenceLevel(double score)
        {
            return score switch
            {
                >= 0.80 => ConfidenceLevel.High,
                >= 0.60 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        /// <summary>
        /// Gets the CSS class name for a confidence level.
        /// </summary>
        /// <param name="level">The confidence level.</param>
        /// <returns>The CSS class name.</returns>
        public static string GetCssClass(ConfidenceLevel level)
        {
            return level switch
            {
                ConfidenceLevel.High => "confidence-high",
                ConfidenceLevel.Medium => "confidence-medium",
                ConfidenceLevel.Low => "confidence-low",
                _ => "confidence-unknown"
            };
        }

        /// <summary>
        /// Gets the CSS class name for a confidence score.
        /// </summary>
        /// <param name="score">The confidence score (0.0 to 1.0).</param>
        /// <returns>The CSS class name.</returns>
        public static string GetCssClass(double score)
        {
            return GetCssClass(GetConfidenceLevel(score));
        }

        /// <summary>
        /// Gets the color code for a confidence level.
        /// </summary>
        /// <param name="level">The confidence level.</param>
        /// <returns>The color code in hex format.</returns>
        public static string GetColorCode(ConfidenceLevel level)
        {
            return level switch
            {
                ConfidenceLevel.High => "#10b981",   // Green
                ConfidenceLevel.Medium => "#f59e0b",  // Amber
                ConfidenceLevel.Low => "#ef4444",     // Red
                _ => "#6b7280"                        // Gray
            };
        }

        /// <summary>
        /// Gets the color code for a confidence score.
        /// </summary>
        /// <param name="score">The confidence score (0.0 to 1.0).</param>
        /// <returns>The color code in hex format.</returns>
        public static string GetColorCode(double score)
        {
            return GetColorCode(GetConfidenceLevel(score));
        }

        /// <summary>
        /// Gets a human-readable description of the confidence level.
        /// </summary>
        /// <param name="level">The confidence level.</param>
        /// <returns>A description of the confidence level.</returns>
        public static string GetDescription(ConfidenceLevel level)
        {
            return level switch
            {
                ConfidenceLevel.High => "High confidence - OCR extraction is very reliable",
                ConfidenceLevel.Medium => "Medium confidence - OCR extraction may need review",
                ConfidenceLevel.Low => "Low confidence - Manual verification recommended",
                _ => "Unknown confidence level"
            };
        }

        /// <summary>
        /// Gets a human-readable description of the confidence score.
        /// </summary>
        /// <param name="score">The confidence score (0.0 to 1.0).</param>
        /// <returns>A description of the confidence level.</returns>
        public static string GetDescription(double score)
        {
            return GetDescription(GetConfidenceLevel(score));
        }

        /// <summary>
        /// Formats a confidence score as a percentage string.
        /// </summary>
        /// <param name="score">The confidence score (0.0 to 1.0).</param>
        /// <returns>The formatted percentage string.</returns>
        public static string FormatAsPercentage(double score)
        {
            return $"{score:P0}";
        }

        /// <summary>
        /// Gets the ARIA label for accessibility.
        /// </summary>
        /// <param name="score">The confidence score (0.0 to 1.0).</param>
        /// <returns>The ARIA label text.</returns>
        public static string GetAriaLabel(double score)
        {
            var level = GetConfidenceLevel(score);
            var percentage = FormatAsPercentage(score);
            return $"Confidence: {percentage} ({level})";
        }

        /// <summary>
        /// Calculates the average confidence score from multiple scores.
        /// </summary>
        /// <param name="scores">Array of confidence scores.</param>
        /// <returns>The average confidence score.</returns>
        public static double CalculateAverageConfidence(params double[] scores)
        {
            if (scores == null || scores.Length == 0)
                return 0.0;

            double sum = 0;
            foreach (var score in scores)
            {
                sum += Math.Max(0, Math.Min(1, score)); // Clamp to 0-1 range
            }
            return sum / scores.Length;
        }

        /// <summary>
        /// Calculates a weighted average confidence score.
        /// </summary>
        /// <param name="scoresAndWeights">Tuples of (score, weight).</param>
        /// <returns>The weighted average confidence score.</returns>
        public static double CalculateWeightedConfidence(params (double score, double weight)[] scoresAndWeights)
        {
            if (scoresAndWeights == null || scoresAndWeights.Length == 0)
                return 0.0;

            double weightedSum = 0;
            double totalWeight = 0;

            foreach (var (score, weight) in scoresAndWeights)
            {
                var clampedScore = Math.Max(0, Math.Min(1, score));
                var clampedWeight = Math.Max(0, weight);
                weightedSum += clampedScore * clampedWeight;
                totalWeight += clampedWeight;
            }

            return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
        }
    }
}