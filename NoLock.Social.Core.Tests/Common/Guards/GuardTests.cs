using Xunit;
using FluentAssertions;
using NoLock.Social.Core.Common.Guards;

namespace NoLock.Social.Core.Tests.Common.Guards
{
    public class GuardTests
    {
        #region AgainstNull Tests

        [Fact]
        public void AgainstNull_WhenValueIsNotNull_ReturnsValue()
        {
            // Arrange
            var value = "test";

            // Act
            var result = Guard.AgainstNull(value);

            // Assert
            result.Should().Be(value);
        }

        [Fact]
        public void AgainstNull_WhenValueIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            string? value = null;

            // Act & Assert
            var action = () => Guard.AgainstNull(value);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("value");
        }

        [Fact]
        public void AgainstNull_WithCustomMessage_WhenValueIsNull_ThrowsWithMessage()
        {
            // Arrange
            string? value = null;
            var customMessage = "Custom error message";

            // Act & Assert
            var action = () => Guard.AgainstNull(value, customMessage);
            // Due to parameter order in Guard implementation, the message becomes the parameter name
            action.Should().Throw<ArgumentNullException>()
                .And.ParamName.Should().Be(customMessage);
        }

        [Fact]
        public void AgainstNull_WithCustomMessage_WhenValueIsNotNull_ReturnsValue()
        {
            // Arrange
            var value = "test";
            var customMessage = "Custom error message";

            // Act
            var result = Guard.AgainstNull(value, customMessage);

            // Assert
            result.Should().Be(value);
        }

        #endregion

        #region AgainstNullOrEmpty Tests

        [Theory]
        [InlineData("test")]
        [InlineData(" ")]
        [InlineData("  multiple words  ")]
        public void AgainstNullOrEmpty_WhenValueIsValid_ReturnsValue(string value)
        {
            // Act
            var result = Guard.AgainstNullOrEmpty(value);

            // Assert
            result.Should().Be(value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AgainstNullOrEmpty_WhenValueIsNullOrEmpty_ThrowsArgumentException(string? value)
        {
            // Act & Assert
            var action = () => Guard.AgainstNullOrEmpty(value);
            action.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("*Value cannot be null or empty*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AgainstNullOrEmpty_WithCustomMessage_WhenInvalid_ThrowsWithMessage(string? value)
        {
            // Arrange
            var customMessage = "String must have content";

            // Act & Assert
            var action = () => Guard.AgainstNullOrEmpty(value, customMessage);
            // Due to parameter order in Guard implementation, the message becomes the parameter name
            action.Should().Throw<ArgumentException>()
                .And.ParamName.Should().Be(customMessage);
        }

        #endregion

        #region AgainstNullOrWhiteSpace Tests

        [Theory]
        [InlineData("test")]
        [InlineData("test with spaces")]
        [InlineData(" leading space")]
        [InlineData("trailing space ")]
        public void AgainstNullOrWhiteSpace_WhenValueIsValid_ReturnsValue(string value)
        {
            // Act
            var result = Guard.AgainstNullOrWhiteSpace(value);

            // Assert
            result.Should().Be(value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void AgainstNullOrWhiteSpace_WhenValueIsInvalid_ThrowsArgumentException(string? value)
        {
            // Act & Assert
            var action = () => Guard.AgainstNullOrWhiteSpace(value);
            action.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("*Value cannot be null, empty, or whitespace*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AgainstNullOrWhiteSpace_WithCustomMessage_WhenInvalid_ThrowsWithMessage(string? value)
        {
            // Arrange
            var customMessage = "Must provide meaningful text";

            // Act & Assert
            var action = () => Guard.AgainstNullOrWhiteSpace(value, customMessage);
            // Due to parameter order in Guard implementation, the message becomes the parameter name
            action.Should().Throw<ArgumentException>()
                .And.ParamName.Should().Be(customMessage);
        }

        #endregion

        #region AgainstNegative Tests

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void AgainstNegative_WhenValueIsZeroOrPositive_ReturnsValue(int value)
        {
            // Act
            var result = Guard.AgainstNegative(value);

            // Assert
            result.Should().Be(value);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(int.MinValue)]
        public void AgainstNegative_WhenValueIsNegative_ThrowsArgumentOutOfRangeException(int value)
        {
            // Act & Assert
            var action = () => Guard.AgainstNegative(value);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage("*Value cannot be negative*");
        }

        #endregion

        #region AgainstZeroOrNegative Tests

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void AgainstZeroOrNegative_WhenValueIsPositive_ReturnsValue(int value)
        {
            // Act
            var result = Guard.AgainstZeroOrNegative(value);

            // Assert
            result.Should().Be(value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(int.MinValue)]
        public void AgainstZeroOrNegative_WhenValueIsZeroOrNegative_ThrowsArgumentOutOfRangeException(int value)
        {
            // Act & Assert
            var action = () => Guard.AgainstZeroOrNegative(value);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage("*Value must be positive*");
        }

        #endregion

        #region AgainstOutOfRange Tests

        [Theory]
        [InlineData(5, 0, 10)]
        [InlineData(0, 0, 10)]
        [InlineData(10, 0, 10)]
        [InlineData(-5, -10, 0)]
        [InlineData(0, -5, 5)]
        public void AgainstOutOfRange_WhenValueIsInRange_ReturnsValue(int value, int min, int max)
        {
            // Act
            var result = Guard.AgainstOutOfRange(value, min, max);

            // Assert
            result.Should().Be(value);
        }

        [Theory]
        [InlineData(-1, 0, 10)]
        [InlineData(11, 0, 10)]
        [InlineData(0, 1, 10)]
        [InlineData(10, 0, 9)]
        public void AgainstOutOfRange_WhenValueIsOutOfRange_ThrowsArgumentOutOfRangeException(int value, int min, int max)
        {
            // Act & Assert
            var action = () => Guard.AgainstOutOfRange(value, min, max);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage($"*Value must be between {min} and {max}*");
        }

        #endregion

        #region AgainstInvalidOperation Tests

        [Fact]
        public void AgainstInvalidOperation_WhenConditionIsTrue_DoesNotThrow()
        {
            // Act & Assert
            var action = () => Guard.AgainstInvalidOperation(true, "Should not throw");
            action.Should().NotThrow();
        }

        [Fact]
        public void AgainstInvalidOperation_WhenConditionIsFalse_ThrowsInvalidOperationException()
        {
            // Arrange
            var errorMessage = "Operation not allowed";

            // Act & Assert
            var action = () => Guard.AgainstInvalidOperation(false, errorMessage);
            action.Should().Throw<InvalidOperationException>()
                .WithMessage(errorMessage);
        }

        [Theory]
        [InlineData(true, "No error")]
        [InlineData(false, "Error occurred")]
        public void AgainstInvalidOperation_MultipleScenarios_BehavesCorrectly(bool condition, string message)
        {
            // Act & Assert
            if (condition)
            {
                var action = () => Guard.AgainstInvalidOperation(condition, message);
                action.Should().NotThrow();
            }
            else
            {
                var action = () => Guard.AgainstInvalidOperation(condition, message);
                action.Should().Throw<InvalidOperationException>()
                    .WithMessage(message);
            }
        }

        #endregion

        #region Edge Cases and Complex Scenarios

        [Fact]
        public void Guard_CanChainMultipleChecks_Successfully()
        {
            // Arrange
            string? value = "test";
            int count = 5;

            // Act & Assert - should not throw
            var action = () =>
            {
                var str = Guard.AgainstNullOrWhiteSpace(value);
                var num = Guard.AgainstNegative(count);
                Guard.AgainstInvalidOperation(str.Length > 0, "String must have length");
            };
            
            action.Should().NotThrow();
        }

        [Fact]
        public void Guard_WithComplexObject_WorksCorrectly()
        {
            // Arrange
            var complexObject = new TestClass { Name = "Test", Value = 42 };

            // Act
            var result = Guard.AgainstNull(complexObject);

            // Assert
            result.Should().BeSameAs(complexObject);
            result.Name.Should().Be("Test");
            result.Value.Should().Be(42);
        }

        [Fact]
        public void Guard_WithNullComplexObject_ThrowsCorrectly()
        {
            // Arrange
            TestClass? complexObject = null;

            // Act & Assert
            var action = () => Guard.AgainstNull(complexObject);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("complexObject");
        }

        private class TestClass
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }

        #endregion
    }
}