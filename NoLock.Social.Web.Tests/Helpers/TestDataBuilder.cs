using System;
using System.Collections.Generic;

namespace NoLock.Social.Web.Tests.Helpers
{
    /// <summary>
    /// Builder class for creating test data objects
    /// </summary>
    public static class TestDataBuilder
    {

        /// <summary>
        /// Creates test image data
        /// </summary>
        public static byte[] CreateTestImageData()
        {
            // Simple 1x1 pixel PNG for testing
            return Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        }

        /// <summary>
        /// Creates a base64 encoded test image
        /// </summary>
        public static string CreateBase64TestImage()
        {
            return $"data:image/png;base64,{Convert.ToBase64String(CreateTestImageData())}";
        }

    }
}