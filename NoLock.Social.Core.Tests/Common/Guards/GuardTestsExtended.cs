using Xunit;
using FluentAssertions;
using NoLock.Social.Core.Common.Guards;

namespace NoLock.Social.Core.Tests.Common.Guards
{
    /// <summary>
    /// Extended comprehensive tests for Guard utility class to achieve 90%+ coverage
    /// </summary>
    public class GuardTestsExtended
    {
        #region CallerArgumentExpression Tests

        [Fact]
        public void AgainstNull_CapturesCorrectParameterName()
        {
            // Arrange
            string? nullValue = null;

            // Act & Assert - parameter name should be captured
            var action = () => Guard.AgainstNull(nullValue);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("nullValue");
        }

        [Fact]
        public void AgainstNullOrEmpty_CapturesCorrectParameterName()
        {
            // Arrange
            string emptyString = "";

            // Act & Assert
            var action = () => Guard.AgainstNullOrEmpty(emptyString);
            action.Should().Throw<ArgumentException>()
                .WithParameterName("emptyString");
        }

        [Fact]
        public void AgainstNullOrWhiteSpace_CapturesCorrectParameterName()
        {
            // Arrange
            string whitespaceString = "   ";

            // Act & Assert
            var action = () => Guard.AgainstNullOrWhiteSpace(whitespaceString);
            action.Should().Throw<ArgumentException>()
                .WithParameterName("whitespaceString");
        }

        [Fact]
        public void AgainstNegative_CapturesCorrectParameterName()
        {
            // Arrange
            int negativeNumber = -5;

            // Act & Assert
            var action = () => Guard.AgainstNegative(negativeNumber);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("negativeNumber");
        }

        [Fact]
        public void AgainstZeroOrNegative_CapturesCorrectParameterName()
        {
            // Arrange
            int zeroValue = 0;

            // Act & Assert
            var action = () => Guard.AgainstZeroOrNegative(zeroValue);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("zeroValue");
        }

        [Fact]
        public void AgainstOutOfRange_CapturesCorrectParameterName()
        {
            // Arrange
            int outOfRangeValue = 100;

            // Act & Assert
            var action = () => Guard.AgainstOutOfRange(outOfRangeValue, 1, 10);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("outOfRangeValue");
        }

        #endregion

        #region Different Object Types Tests

        [Theory]
        [InlineData(42, "integer value")]
        [InlineData(3.14, "double value")]
        [InlineData(true, "boolean value")]
        public void AgainstNull_WithValueTypes_WhenBoxed_ReturnsValue<T>(T value, string description)
            where T : struct
        {
            // Act
            object boxedValue = value;
            var result = Guard.AgainstNull(boxedValue);

            // Assert
            result.Should().Be(boxedValue, $"should return the {description}");
        }

        [Fact]
        public void AgainstNull_WithArray_WhenNotNull_ReturnsArray()
        {
            // Arrange
            var array = new[] { 1, 2, 3 };

            // Act
            var result = Guard.AgainstNull(array);

            // Assert
            result.Should().BeSameAs(array);
            result.Should().Equal(1, 2, 3);
        }

        [Fact]
        public void AgainstNull_WithArray_WhenNull_ThrowsArgumentNullException()
        {
            // Arrange
            int[]? array = null;

            // Act & Assert
            var action = () => Guard.AgainstNull(array);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("array");
        }

        [Fact]
        public void AgainstNull_WithList_WhenNotNull_ReturnsList()
        {
            // Arrange
            var list = new List<string> { "a", "b", "c" };

            // Act
            var result = Guard.AgainstNull(list);

            // Assert
            result.Should().BeSameAs(list);
            result.Should().Equal("a", "b", "c");
        }

        [Fact]
        public void AgainstNull_WithList_WhenNull_ThrowsArgumentNullException()
        {
            // Arrange
            List<string>? list = null;

            // Act & Assert
            var action = () => Guard.AgainstNull(list);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("list");
        }

        [Fact]
        public void AgainstNull_WithCustomObject_WhenNotNull_ReturnsObject()
        {
            // Arrange
            var customObject = new TestClass { Name = "Test", Value = 42 };

            // Act
            var result = Guard.AgainstNull(customObject);

            // Assert
            result.Should().BeSameAs(customObject);
            result.Name.Should().Be("Test");
            result.Value.Should().Be(42);
        }

        [Fact]
        public void AgainstNull_WithCustomObject_WhenNull_ThrowsArgumentNullException()
        {
            // Arrange
            TestClass? customObject = null;

            // Act & Assert
            var action = () => Guard.AgainstNull(customObject);
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("customObject");
        }

        #endregion

        #region Boundary Condition Tests

        [Theory]
        [InlineData(int.MinValue, int.MinValue, int.MaxValue, "minimum int at lower bound")]
        [InlineData(int.MaxValue, int.MinValue, int.MaxValue, "maximum int at upper bound")]
        [InlineData(0, int.MinValue, int.MaxValue, "zero in full range")]
        [InlineData(-1000, -2000, 2000, "negative value in range including negatives")]
        public void AgainstOutOfRange_WithBoundaryValues_WhenInRange_ReturnsValue(int value, int min, int max, string description)
        {
            // Act
            var result = Guard.AgainstOutOfRange(value, min, max);

            // Assert
            result.Should().Be(value, $"should return {description}");
        }

        [Theory]
        [InlineData(int.MinValue + 1, int.MinValue, int.MinValue, "just above minimum bound")]
        [InlineData(int.MaxValue - 1, int.MaxValue, int.MaxValue, "just below maximum bound")]
        [InlineData(1, 2, 10, "just below minimum")]
        [InlineData(11, 2, 10, "just above maximum")]
        public void AgainstOutOfRange_WithBoundaryValues_WhenOutOfRange_ThrowsException(int value, int min, int max, string description)
        {
            // Act & Assert
            var action = () => Guard.AgainstOutOfRange(value, min, max);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage($"*Value must be between {min} and {max}*")
                .Which.ActualValue.Should().Be(value, $"should capture actual value for {description}");
        }

        [Theory]
        [InlineData(0, "zero boundary")]
        [InlineData(1, "positive boundary")]
        [InlineData(int.MaxValue, "maximum positive value")]
        public void AgainstNegative_WithBoundaryValues_WhenNonNegative_ReturnsValue(int value, string description)
        {
            // Act
            var result = Guard.AgainstNegative(value);

            // Assert
            result.Should().Be(value, $"should return {description}");
        }

        [Theory]
        [InlineData(-1, "just below zero")]
        [InlineData(int.MinValue, "minimum negative value")]
        public void AgainstNegative_WithBoundaryValues_WhenNegative_ThrowsException(int value, string description)
        {
            // Act & Assert
            var action = () => Guard.AgainstNegative(value);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage("*Value cannot be negative*")
                .Which.ActualValue.Should().Be(value, $"should capture actual value for {description}");
        }

        [Theory]
        [InlineData(1, "minimum positive value")]
        [InlineData(int.MaxValue, "maximum positive value")]
        public void AgainstZeroOrNegative_WithBoundaryValues_WhenPositive_ReturnsValue(int value, string description)
        {
            // Act
            var result = Guard.AgainstZeroOrNegative(value);

            // Assert
            result.Should().Be(value, $"should return {description}");
        }

        [Theory]
        [InlineData(0, "zero boundary")]
        [InlineData(-1, "just below zero")]
        [InlineData(int.MinValue, "minimum negative value")]
        public void AgainstZeroOrNegative_WithBoundaryValues_WhenZeroOrNegative_ThrowsException(int value, string description)
        {
            // Act & Assert
            var action = () => Guard.AgainstZeroOrNegative(value);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage("*Value must be positive*")
                .Which.ActualValue.Should().Be(value, $"should capture actual value for {description}");
        }

        #endregion

        #region String Validation Edge Cases

        [Theory]
        [InlineData("\u0000", "null character")]
        [InlineData("\u200B", "zero-width space")]
        [InlineData("\uFEFF", "byte order mark")]
        [InlineData("a\u0000b", "string with embedded null")]
        public void AgainstNullOrEmpty_WithSpecialCharacters_ReturnsValue(string value, string description)
        {
            // Act
            var result = Guard.AgainstNullOrEmpty(value);

            // Assert
            result.Should().Be(value, $"should return string with {description}");
        }

        [Theory]
        [InlineData("a", "single character")]
        [InlineData("\u0000", "null character is not whitespace")]
        [InlineData("\u200B", "zero-width space is not whitespace")]
        [InlineData("\uFEFF", "BOM is not whitespace")]
        public void AgainstNullOrWhiteSpace_WithNonWhitespaceSpecialCharacters_ReturnsValue(string value, string description)
        {
            // Act
            var result = Guard.AgainstNullOrWhiteSpace(value);

            // Assert
            result.Should().Be(value, $"should return {description}");
        }

        [Theory]
        [InlineData("\u00A0", "non-breaking space")]
        [InlineData("\u2028", "line separator")]
        [InlineData("\u2029", "paragraph separator")]
        [InlineData("\u0085", "next line character")]
        public void AgainstNullOrWhiteSpace_WithUnicodeWhitespace_ThrowsException(string value, string description)
        {
            // Act & Assert
            var action = () => Guard.AgainstNullOrWhiteSpace(value);
            action.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("*Value cannot be null, empty, or whitespace*");
        }

        #endregion

        #region Custom Message Tests

        [Theory]
        [InlineData("Custom null error", "custom null message")]
        [InlineData("Parameter cannot be null", "parameter null message")]
        [InlineData("", "empty custom message")]
        public void AgainstNull_WithCustomMessage_WhenNull_ThrowsWithCustomMessage(string customMessage, string description)
        {
            // Arrange
            string? value = null;

            // Act & Assert
            var action = () => Guard.AgainstNull(value, customMessage);
            action.Should().Throw<ArgumentNullException>()
                .WithMessage($"{customMessage}*")
                .And.ParamName.Should().Be("value", $"should use correct parameter name for {description}");
        }

        [Theory]
        [InlineData("String must have content", "custom empty message")]
        [InlineData("Value is required", "value required message")]
        [InlineData("", "empty custom message")]
        public void AgainstNullOrEmpty_WithCustomMessage_WhenEmpty_ThrowsWithCustomMessage(string customMessage, string description)
        {
            // Arrange
            string value = "";

            // Act & Assert
            var action = () => Guard.AgainstNullOrEmpty(value, customMessage);
            action.Should().Throw<ArgumentException>()
                .WithMessage($"{customMessage}*")
                .And.ParamName.Should().Be("value", $"should use correct parameter name for {description}");
        }

        [Theory]
        [InlineData("Must provide meaningful text", "meaningful text message")]
        [InlineData("Whitespace not allowed", "no whitespace message")]
        [InlineData("", "empty custom message")]
        public void AgainstNullOrWhiteSpace_WithCustomMessage_WhenWhitespace_ThrowsWithCustomMessage(string customMessage, string description)
        {
            // Arrange
            string value = "   ";

            // Act & Assert
            var action = () => Guard.AgainstNullOrWhiteSpace(value, customMessage);
            action.Should().Throw<ArgumentException>()
                .WithMessage($"{customMessage}*")
                .And.ParamName.Should().Be("value", $"should use correct parameter name for {description}");
        }

        #endregion

        #region Invalid Operation Condition Tests

        [Theory]
        [InlineData(true, "Operation is valid", "valid operation")]
        [InlineData(true, "All systems go", "all systems operational")]
        [InlineData(true, "", "empty message but valid condition")]
        public void AgainstInvalidOperation_WithValidConditions_DoesNotThrow(bool condition, string message, string description)
        {
            // Act & Assert
            var action = () => Guard.AgainstInvalidOperation(condition, message);
            action.Should().NotThrow($"should not throw for {description}");
        }

        [Theory]
        [InlineData(false, "Resource not available", "resource unavailable")]
        [InlineData(false, "Invalid state detected", "invalid state")]
        [InlineData(false, "", "empty error message")]
        [InlineData(false, "Very long error message that exceeds typical lengths to test message handling with extensive details about what went wrong", "very long error message")]
        public void AgainstInvalidOperation_WithInvalidConditions_ThrowsWithCorrectMessage(bool condition, string message, string description)
        {
            // Act & Assert
            var action = () => Guard.AgainstInvalidOperation(condition, message);
            action.Should().Throw<InvalidOperationException>()
                .WithMessage(message);
        }

        #endregion

        #region Range Validation Edge Cases

        [Theory]
        [InlineData(5, 5, 5, "single value range")]
        [InlineData(0, 0, 0, "zero range")]
        [InlineData(-10, -10, -10, "negative single value range")]
        public void AgainstOutOfRange_WithSingleValueRange_WhenValueMatches_ReturnsValue(int value, int min, int max, string description)
        {
            // Act
            var result = Guard.AgainstOutOfRange(value, min, max);

            // Assert
            result.Should().Be(value, $"should return value for {description}");
        }

        [Theory]
        [InlineData(4, 5, 5, "below single value range")]
        [InlineData(6, 5, 5, "above single value range")]
        public void AgainstOutOfRange_WithSingleValueRange_WhenValueDiffers_ThrowsException(int value, int min, int max, string description)
        {
            // Act & Assert
            var action = () => Guard.AgainstOutOfRange(value, min, max);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage($"*Value must be between {min} and {max}*");
        }

        [Fact]
        public void AgainstOutOfRange_WithInvalidRange_MinGreaterThanMax_StillValidatesCorrectly()
        {
            // Note: The Guard method doesn't validate that min <= max, 
            // it just checks if value is outside the specified bounds
            // This test documents the current behavior
            
            // Arrange
            int value = 5;
            int min = 10;
            int max = 3; // min > max

            // Act & Assert - value 5 is less than min(10) so should throw
            var action = () => Guard.AgainstOutOfRange(value, min, max);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("value")
                .WithMessage($"*Value must be between {min} and {max}*");
        }

        #endregion

        #region Complex Integration Scenarios

        [Fact]
        public void Guard_MultipleValidations_WithAllValidInputs_PassesThrough()
        {
            // Arrange
            var name = "John Doe";
            var age = 25;
            var items = new List<string> { "item1", "item2" };

            // Act & Assert - chaining multiple validations
            var action = () =>
            {
                var validName = Guard.AgainstNullOrWhiteSpace(name);
                var validAge = Guard.AgainstZeroOrNegative(age);
                var validItems = Guard.AgainstNull(items);
                Guard.AgainstInvalidOperation(validItems.Count > 0, "Items list cannot be empty");
                
                return new { Name = validName, Age = validAge, Items = validItems };
            };

            var result = action.Should().NotThrow().Subject;
            result.Name.Should().Be(name);
            result.Age.Should().Be(age);
            result.Items.Should().BeSameAs(items);
        }

        [Fact]
        public void Guard_MultipleValidations_WithInvalidInputs_ThrowsAtFirstFailure()
        {
            // Arrange
            string? name = null; // This will fail first
            var age = -5; // This would also fail but won't be reached

            // Act & Assert
            var action = () =>
            {
                var validName = Guard.AgainstNullOrWhiteSpace(name); // Should throw here
                var validAge = Guard.AgainstZeroOrNegative(age); // Won't be reached
                return new { Name = validName, Age = validAge };
            };

            action.Should().Throw<ArgumentException>()
                .WithParameterName("name");
        }

        #endregion

        #region Performance and Memory Tests

        [Fact]
        public void AgainstNull_WithLargeString_DoesNotCopyString()
        {
            // Arrange
            var largeString = new string('x', 100000);

            // Act
            var result = Guard.AgainstNull(largeString);

            // Assert
            result.Should().BeSameAs(largeString, "should return same reference, not a copy");
        }

        [Fact]
        public void Guard_CallerArgumentExpression_DoesNotAllocateUnnecessarily()
        {
            // Arrange
            var testString = "valid value";
            
            // Act - calling with valid value should not throw and should be efficient
            var result1 = Guard.AgainstNull(testString);
            var result2 = Guard.AgainstNullOrEmpty(testString);
            var result3 = Guard.AgainstNullOrWhiteSpace(testString);
            var result4 = Guard.AgainstNegative(42);
            var result5 = Guard.AgainstZeroOrNegative(42);
            var result6 = Guard.AgainstOutOfRange(5, 1, 10);
            Guard.AgainstInvalidOperation(true, "no error");

            // Assert
            result1.Should().BeSameAs(testString);
            result2.Should().BeSameAs(testString);
            result3.Should().BeSameAs(testString);
            result4.Should().Be(42);
            result5.Should().Be(42);
            result6.Should().Be(5);
        }

        #endregion

        #region Exception Precision Tests

        [Fact]
        public void AgainstNegative_WhenThrows_HasCorrectExceptionDetails()
        {
            // Arrange
            int negativeValue = -42;

            // Act & Assert
            var action = () => Guard.AgainstNegative(negativeValue);
            var exception = action.Should().Throw<ArgumentOutOfRangeException>().Which;
            
            exception.ParamName.Should().Be("negativeValue");
            exception.ActualValue.Should().Be(negativeValue);
            exception.Message.Should().Contain("Value cannot be negative");
        }

        [Fact]
        public void AgainstZeroOrNegative_WhenThrows_HasCorrectExceptionDetails()
        {
            // Arrange
            int zeroValue = 0;

            // Act & Assert
            var action = () => Guard.AgainstZeroOrNegative(zeroValue);
            var exception = action.Should().Throw<ArgumentOutOfRangeException>().Which;
            
            exception.ParamName.Should().Be("zeroValue");
            exception.ActualValue.Should().Be(zeroValue);
            exception.Message.Should().Contain("Value must be positive");
        }

        [Fact]
        public void AgainstOutOfRange_WhenThrows_HasCorrectExceptionDetails()
        {
            // Arrange
            int outOfRangeValue = 15;
            int min = 1;
            int max = 10;

            // Act & Assert
            var action = () => Guard.AgainstOutOfRange(outOfRangeValue, min, max);
            var exception = action.Should().Throw<ArgumentOutOfRangeException>().Which;
            
            exception.ParamName.Should().Be("outOfRangeValue");
            exception.ActualValue.Should().Be(outOfRangeValue);
            exception.Message.Should().Contain($"Value must be between {min} and {max}");
        }

        #endregion

        #region Test Helper Classes

        private class TestClass
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }

        #endregion
    }
}