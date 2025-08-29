using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.Tests.OCR.Models
{
    /// <summary>
    /// Unit tests for the BoundingBox model.
    /// </summary>
    public class BoundingBoxTests
    {
        [Theory]
        [InlineData(0, 0, 100, 50, 1, "Standard bounding box")]
        [InlineData(10, 20, 200, 150, 2, "Box on second page")]
        [InlineData(-10, -20, 100, 50, 1, "Box with negative coordinates")]
        [InlineData(0, 0, 0, 0, 1, "Zero-size bounding box")]
        [InlineData(int.MaxValue, int.MaxValue, 100, 50, 1, "Box at max coordinates")]
        public void BoundingBox_PropertiesSetCorrectly(int x, int y, int width, int height, int pageNumber, string scenario)
        {
            // Arrange & Act
            var boundingBox = new BoundingBox
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                PageNumber = pageNumber
            };

            // Assert
            Assert.Equal(x, boundingBox.X);
            Assert.Equal(y, boundingBox.Y);
            Assert.Equal(width, boundingBox.Width);
            Assert.Equal(height, boundingBox.Height);
            Assert.Equal(pageNumber, boundingBox.PageNumber);
        }

        [Fact]
        public void BoundingBox_DefaultValues_AreZero()
        {
            // Arrange & Act
            var boundingBox = new BoundingBox();

            // Assert
            Assert.Equal(0, boundingBox.X);
            Assert.Equal(0, boundingBox.Y);
            Assert.Equal(0, boundingBox.Width);
            Assert.Equal(0, boundingBox.Height);
            Assert.Equal(0, boundingBox.PageNumber);
        }

        [Theory]
        [InlineData(100, 200, 300, "Right boundary calculation")]
        [InlineData(0, 50, 50, "Right boundary at origin")]
        [InlineData(-100, 200, 100, "Right boundary with negative X")]
        public void BoundingBox_RightBoundary_CalculatedCorrectly(int x, int width, int expectedRight, string scenario)
        {
            // Arrange
            var boundingBox = new BoundingBox { X = x, Width = width };

            // Act
            var right = boundingBox.X + boundingBox.Width;

            // Assert
            Assert.Equal(expectedRight, right);
        }

        [Theory]
        [InlineData(100, 200, 300, "Bottom boundary calculation")]
        [InlineData(0, 50, 50, "Bottom boundary at origin")]
        [InlineData(-100, 200, 100, "Bottom boundary with negative Y")]
        public void BoundingBox_BottomBoundary_CalculatedCorrectly(int y, int height, int expectedBottom, string scenario)
        {
            // Arrange
            var boundingBox = new BoundingBox { Y = y, Height = height };

            // Act
            var bottom = boundingBox.Y + boundingBox.Height;

            // Assert
            Assert.Equal(expectedBottom, bottom);
        }

        [Theory]
        [InlineData(100, 50, 5000, "Standard area calculation")]
        [InlineData(0, 0, 0, "Zero area")]
        [InlineData(int.MaxValue / 2, 2, int.MaxValue - 1, "Large area calculation")]
        public void BoundingBox_Area_CalculatedCorrectly(int width, int height, long expectedArea, string scenario)
        {
            // Arrange
            var boundingBox = new BoundingBox { Width = width, Height = height };

            // Act
            long area = (long)boundingBox.Width * boundingBox.Height;

            // Assert
            Assert.Equal(expectedArea, area);
        }

        [Theory]
        [InlineData(0, 0, 100, 100, 50, 50, true, "Point inside box")]
        [InlineData(0, 0, 100, 100, 0, 0, true, "Point at top-left corner")]
        [InlineData(0, 0, 100, 100, 100, 100, true, "Point at bottom-right corner")]
        [InlineData(0, 0, 100, 100, 101, 50, false, "Point outside right edge")]
        [InlineData(0, 0, 100, 100, 50, 101, false, "Point outside bottom edge")]
        [InlineData(0, 0, 100, 100, -1, 50, false, "Point outside left edge")]
        [InlineData(0, 0, 100, 100, 50, -1, false, "Point outside top edge")]
        public void BoundingBox_ContainsPoint_WorksCorrectly(int x, int y, int width, int height, 
            int pointX, int pointY, bool expectedContains, string scenario)
        {
            // Arrange
            var boundingBox = new BoundingBox 
            { 
                X = x, 
                Y = y, 
                Width = width, 
                Height = height 
            };

            // Act
            bool contains = pointX >= boundingBox.X && 
                          pointX <= (boundingBox.X + boundingBox.Width) &&
                          pointY >= boundingBox.Y && 
                          pointY <= (boundingBox.Y + boundingBox.Height);

            // Assert
            Assert.Equal(expectedContains, contains);
        }

        [Fact]
        public void BoundingBox_MultipleInstances_AreIndependent()
        {
            // Arrange
            var box1 = new BoundingBox { X = 10, Y = 20, Width = 30, Height = 40, PageNumber = 1 };
            var box2 = new BoundingBox { X = 50, Y = 60, Width = 70, Height = 80, PageNumber = 2 };

            // Assert
            Assert.NotEqual(box1.X, box2.X);
            Assert.NotEqual(box1.Y, box2.Y);
            Assert.NotEqual(box1.Width, box2.Width);
            Assert.NotEqual(box1.Height, box2.Height);
            Assert.NotEqual(box1.PageNumber, box2.PageNumber);
        }

        [Theory]
        [InlineData(-100, -50, 200, 100, "Negative coordinates with positive dimensions")]
        [InlineData(0, 0, -100, -50, "Negative dimensions (invalid but settable)")]
        [InlineData(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue, "Extreme values")]
        public void BoundingBox_EdgeCases_HandledCorrectly(int x, int y, int width, int height, string scenario)
        {
            // Arrange & Act
            var boundingBox = new BoundingBox
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };

            // Assert - Properties should be set as provided
            Assert.Equal(x, boundingBox.X);
            Assert.Equal(y, boundingBox.Y);
            Assert.Equal(width, boundingBox.Width);
            Assert.Equal(height, boundingBox.Height);
        }
    }
}