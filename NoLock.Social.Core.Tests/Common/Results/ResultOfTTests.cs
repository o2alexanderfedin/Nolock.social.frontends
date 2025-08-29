using Xunit;
using FluentAssertions;
using NoLock.Social.Core.Common.Results;

namespace NoLock.Social.Core.Tests.Common.Results
{
    public class ResultOfTTests
    {
        #region Success and Failure Creation Tests

        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Arrange
            var value = "test value";

            // Act
            var result = Result<string>.Success(value);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Value.Should().Be(value);
        }

        [Fact]
        public void Success_WithNullValue_CreatesSuccessfulResult()
        {
            // Arrange
            string? value = null;

            // Act
            var result = Result<string?>.Success(value);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Value.Should().BeNull();
        }

        [Fact]
        public void Failure_CreatesFailedResult()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var result = Result<string>.Failure(exception);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().Be(exception);
        }

        #endregion

        #region Property Access Tests

        [Fact]
        public void Value_OnSuccessResult_ReturnsValue()
        {
            // Arrange
            var expectedValue = 42;
            var result = Result<int>.Success(expectedValue);

            // Act
            var value = result.Value;

            // Assert
            value.Should().Be(expectedValue);
        }

        [Fact]
        public void Value_OnFailureResult_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = Result<int>.Failure(new Exception("Test"));

            // Act & Assert
            var action = () => _ = result.Value;
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot access value of failed result");
        }

        [Fact]
        public void Exception_OnFailureResult_ReturnsException()
        {
            // Arrange
            var expectedException = new ArgumentException("Test error");
            var result = Result<int>.Failure(expectedException);

            // Act
            var exception = result.Exception;

            // Assert
            exception.Should().Be(expectedException);
        }

        [Fact]
        public void Exception_OnSuccessResult_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act & Assert
            var action = () => _ = result.Exception;
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot access exception of successful result");
        }

        #endregion

        #region OnSuccess Tests

        [Fact]
        public void OnSuccess_WithSuccessResult_ExecutesAction()
        {
            // Arrange
            var value = "test";
            var result = Result<string>.Success(value);
            var wasExecuted = false;
            string? capturedValue = null;

            // Act
            var chainedResult = result.OnSuccess(v =>
            {
                wasExecuted = true;
                capturedValue = v;
            });

            // Assert
            wasExecuted.Should().BeTrue();
            capturedValue.Should().Be(value);
            chainedResult.Should().Be(result);
        }

        [Fact]
        public void OnSuccess_WithFailureResult_DoesNotExecuteAction()
        {
            // Arrange
            var result = Result<string>.Failure(new Exception("Test"));
            var wasExecuted = false;

            // Act
            var chainedResult = result.OnSuccess(v => wasExecuted = true);

            // Assert
            wasExecuted.Should().BeFalse();
            chainedResult.Should().Be(result);
        }

        #endregion

        #region OnFailure Tests

        [Fact]
        public void OnFailure_WithFailureResult_ExecutesAction()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test");
            var result = Result<string>.Failure(expectedException);
            var wasExecuted = false;
            Exception? capturedException = null;

            // Act
            var chainedResult = result.OnFailure(ex =>
            {
                wasExecuted = true;
                capturedException = ex;
            });

            // Assert
            wasExecuted.Should().BeTrue();
            capturedException.Should().Be(expectedException);
            chainedResult.Should().Be(result);
        }

        [Fact]
        public void OnFailure_WithSuccessResult_DoesNotExecuteAction()
        {
            // Arrange
            var result = Result<string>.Success("test");
            var wasExecuted = false;

            // Act
            var chainedResult = result.OnFailure(ex => wasExecuted = true);

            // Assert
            wasExecuted.Should().BeFalse();
            chainedResult.Should().Be(result);
        }

        #endregion

        #region Map Tests

        [Fact]
        public void Map_WithSuccessResult_TransformsValue()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var mappedResult = result.Map(x => x.ToString());

            // Assert
            mappedResult.IsSuccess.Should().BeTrue();
            mappedResult.Value.Should().Be("42");
        }

        [Fact]
        public void Map_WithFailureResult_PropagatesFailure()
        {
            // Arrange
            var exception = new InvalidOperationException("Test");
            var result = Result<int>.Failure(exception);

            // Act
            var mappedResult = result.Map(x => x.ToString());

            // Assert
            mappedResult.IsFailure.Should().BeTrue();
            mappedResult.Exception.Should().Be(exception);
        }

        [Fact]
        public void Map_ToComplexType_WorksCorrectly()
        {
            // Arrange
            var result = Result<string>.Success("John Doe");

            // Act
            var mappedResult = result.Map(name => new { Name = name, Length = name.Length });

            // Assert
            mappedResult.IsSuccess.Should().BeTrue();
            mappedResult.Value.Name.Should().Be("John Doe");
            mappedResult.Value.Length.Should().Be(8);
        }

        [Fact]
        public void Map_ChainedTransformations_WorkCorrectly()
        {
            // Arrange
            var result = Result<int>.Success(10);

            // Act
            var finalResult = result
                .Map(x => x * 2)
                .Map(x => x + 5)
                .Map(x => x.ToString());

            // Assert
            finalResult.IsSuccess.Should().BeTrue();
            finalResult.Value.Should().Be("25");
        }

        #endregion

        #region Match Tests

        [Fact]
        public void Match_WithSuccessResult_CallsOnSuccess()
        {
            // Arrange
            var result = Result<int>.Success(42);

            // Act
            var matchResult = result.Match(
                onSuccess: value => $"Success: {value}",
                onFailure: ex => $"Failure: {ex.Message}"
            );

            // Assert
            matchResult.Should().Be("Success: 42");
        }

        [Fact]
        public void Match_WithFailureResult_CallsOnFailure()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");
            var result = Result<int>.Failure(exception);

            // Act
            var matchResult = result.Match(
                onSuccess: value => $"Success: {value}",
                onFailure: ex => $"Failure: {ex.Message}"
            );

            // Assert
            matchResult.Should().Be("Failure: Test error");
        }

        [Fact]
        public void Match_WithDifferentReturnTypes_WorksCorrectly()
        {
            // Arrange
            var successResult = Result<string>.Success("test");
            var failureResult = Result<string>.Failure(new Exception("error"));

            // Act
            var successLength = successResult.Match(
                onSuccess: s => s.Length,
                onFailure: _ => -1
            );

            var failureLength = failureResult.Match(
                onSuccess: s => s.Length,
                onFailure: _ => -1
            );

            // Assert
            successLength.Should().Be(4);
            failureLength.Should().Be(-1);
        }

        #endregion

        #region Fluent Chaining Tests

        [Fact]
        public void FluentChaining_OnSuccessAndOnFailure_WorksCorrectly()
        {
            // Arrange
            var successResult = Result<int>.Success(10);
            var failureResult = Result<int>.Failure(new Exception("Error"));
            var successExecuted = false;
            var failureExecuted = false;

            // Act - Success case
            successResult
                .OnSuccess(v => successExecuted = true)
                .OnFailure(ex => failureExecuted = true);

            // Assert
            successExecuted.Should().BeTrue();
            failureExecuted.Should().BeFalse();

            // Reset
            successExecuted = false;
            failureExecuted = false;

            // Act - Failure case
            failureResult
                .OnSuccess(v => successExecuted = true)
                .OnFailure(ex => failureExecuted = true);

            // Assert
            successExecuted.Should().BeFalse();
            failureExecuted.Should().BeTrue();
        }

        #endregion

        #region Value Type Tests

        [Theory]
        [InlineData(42, "42")]
        [InlineData(0, "0")]
        [InlineData(-10, "-10")]
        public void ValueTypes_HandleCorrectly(int input, string expected)
        {
            // Act
            var result = Result<int>.Success(input);
            var mapped = result.Map(x => x.ToString());

            // Assert
            result.Value.Should().Be(input);
            mapped.Value.Should().Be(expected);
        }

        #endregion

        #region Reference Type Tests

        [Fact]
        public void ReferenceTypes_WithNull_HandleCorrectly()
        {
            // Arrange
            string? nullString = null;

            // Act
            var result = Result<string?>.Success(nullString);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeNull();
        }

        [Fact]
        public void ReferenceTypes_WithObject_HandleCorrectly()
        {
            // Arrange
            var obj = new TestClass { Id = 1, Name = "Test" };

            // Act
            var result = Result<TestClass>.Success(obj);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeSameAs(obj);
            result.Value.Id.Should().Be(1);
            result.Value.Name.Should().Be("Test");
        }

        #endregion

        #region Exception Type Tests

        [Theory]
        [InlineData(typeof(ArgumentNullException), "Value cannot be null")]
        [InlineData(typeof(InvalidOperationException), "Invalid operation")]
        [InlineData(typeof(NotSupportedException), "Not supported")]
        public void DifferentExceptionTypes_HandleCorrectly(Type exceptionType, string message)
        {
            // Arrange
            var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;

            // Act
            var result = Result<string>.Failure(exception);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().BeOfType(exceptionType);
            result.Exception.Message.Should().Contain(message);
        }

        #endregion

        #region Struct Behavior Tests

        [Fact]
        public void Result_AsStruct_HasValueSemantics()
        {
            // Arrange
            var result1 = Result<int>.Success(42);
            var result2 = Result<int>.Success(42);

            // Act & Assert
            result1.Should().Be(result2); // Value equality
            result1.GetHashCode().Should().Be(result2.GetHashCode());
        }

        [Fact]
        public void Result_AsStruct_CopiesCorrectly()
        {
            // Arrange
            var original = Result<int>.Success(42);

            // Act
            var copy = original;
            var afterOnSuccess = copy.OnSuccess(_ => { /* no-op */ });

            // Assert
            copy.Value.Should().Be(original.Value);
            afterOnSuccess.Value.Should().Be(original.Value);
        }

        #endregion

        #region Complex Scenarios Tests

        [Fact]
        public void ComplexScenario_ChainedOperationsWithConditionalLogic()
        {
            // Arrange
            var input = 10;
            var executionLog = new List<string>();

            // Act
            var result = Result<int>.Success(input)
                .Map(x =>
                {
                    executionLog.Add($"Doubling {x}");
                    return x * 2;
                })
                .OnSuccess(x => executionLog.Add($"Success checkpoint: {x}"))
                .Map(x =>
                {
                    executionLog.Add($"Adding 5 to {x}");
                    return x + 5;
                })
                .Match(
                    onSuccess: x =>
                    {
                        executionLog.Add($"Final value: {x}");
                        return x;
                    },
                    onFailure: _ =>
                    {
                        executionLog.Add("Failed");
                        return -1;
                    }
                );

            // Assert
            result.Should().Be(25);
            executionLog.Should().BeEquivalentTo(new[]
            {
                "Doubling 10",
                "Success checkpoint: 20",
                "Adding 5 to 20",
                "Final value: 25"
            });
        }

        [Fact]
        public void ComplexScenario_ErrorPropagationThroughChain()
        {
            // Arrange
            var error = new InvalidOperationException("Initial error");
            var executionLog = new List<string>();

            // Act
            var result = Result<int>.Failure(error)
                .Map(x =>
                {
                    executionLog.Add("Should not execute");
                    return x * 2;
                })
                .OnFailure(ex => executionLog.Add($"Error: {ex.Message}"))
                .Map(x =>
                {
                    executionLog.Add("Should not execute either");
                    return x.ToString();
                });

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().Be(error);
            executionLog.Should().BeEquivalentTo(new[]
            {
                "Error: Initial error"
            });
        }

        #endregion

        // Helper class for testing
        private class TestClass
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}