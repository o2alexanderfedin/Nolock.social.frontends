using Xunit;
using FluentAssertions;
using NoLock.Social.Core.Common.Results;

namespace NoLock.Social.Core.Tests.Common.Results
{
    public class ResultTests
    {
        #region Success and Failure Creation Tests

        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Act
            var result = Result.Success();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
        }

        [Fact]
        public void Failure_CreatesFailedResult()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var result = Result.Failure(exception);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().Be(exception);
        }

        #endregion

        #region Property Access Tests

        [Fact]
        public void Exception_OnFailureResult_ReturnsException()
        {
            // Arrange
            var expectedException = new ArgumentException("Test error");
            var result = Result.Failure(expectedException);

            // Act
            var exception = result.Exception;

            // Assert
            exception.Should().Be(expectedException);
        }

        [Fact]
        public void Exception_OnSuccessResult_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = Result.Success();

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
            var result = Result.Success();
            var wasExecuted = false;

            // Act
            var chainedResult = result.OnSuccess(() => wasExecuted = true);

            // Assert
            wasExecuted.Should().BeTrue();
            chainedResult.Should().Be(result);
        }

        [Fact]
        public void OnSuccess_WithFailureResult_DoesNotExecuteAction()
        {
            // Arrange
            var result = Result.Failure(new Exception("Test"));
            var wasExecuted = false;

            // Act
            var chainedResult = result.OnSuccess(() => wasExecuted = true);

            // Assert
            wasExecuted.Should().BeFalse();
            chainedResult.Should().Be(result);
        }

        [Fact]
        public void OnSuccess_WithSideEffects_ExecutesCorrectly()
        {
            // Arrange
            var result = Result.Success();
            var counter = 0;

            // Act
            result
                .OnSuccess(() => counter++)
                .OnSuccess(() => counter++)
                .OnSuccess(() => counter++);

            // Assert
            counter.Should().Be(3);
        }

        #endregion

        #region OnFailure Tests

        [Fact]
        public void OnFailure_WithFailureResult_ExecutesAction()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test");
            var result = Result.Failure(expectedException);
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
            var result = Result.Success();
            var wasExecuted = false;

            // Act
            var chainedResult = result.OnFailure(ex => wasExecuted = true);

            // Assert
            wasExecuted.Should().BeFalse();
            chainedResult.Should().Be(result);
        }

        [Fact]
        public void OnFailure_WithMultipleHandlers_ExecutesAll()
        {
            // Arrange
            var exception = new Exception("Test");
            var result = Result.Failure(exception);
            var executionLog = new List<string>();

            // Act
            result
                .OnFailure(ex => executionLog.Add($"Handler1: {ex.Message}"))
                .OnFailure(ex => executionLog.Add($"Handler2: {ex.Message}"))
                .OnFailure(ex => executionLog.Add($"Handler3: {ex.Message}"));

            // Assert
            executionLog.Should().HaveCount(3);
            executionLog.Should().BeEquivalentTo(new[]
            {
                "Handler1: Test",
                "Handler2: Test",
                "Handler3: Test"
            });
        }

        #endregion

        #region Match Tests

        [Fact]
        public void Match_WithSuccessResult_CallsOnSuccess()
        {
            // Arrange
            var result = Result.Success();

            // Act
            var matchResult = result.Match(
                onSuccess: () => "Success",
                onFailure: ex => $"Failure: {ex.Message}"
            );

            // Assert
            matchResult.Should().Be("Success");
        }

        [Fact]
        public void Match_WithFailureResult_CallsOnFailure()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");
            var result = Result.Failure(exception);

            // Act
            var matchResult = result.Match(
                onSuccess: () => "Success",
                onFailure: ex => $"Failure: {ex.Message}"
            );

            // Assert
            matchResult.Should().Be("Failure: Test error");
        }

        [Fact]
        public void Match_WithDifferentReturnTypes_WorksCorrectly()
        {
            // Arrange
            var successResult = Result.Success();
            var failureResult = Result.Failure(new Exception("error"));

            // Act
            var successValue = successResult.Match(
                onSuccess: () => 42,
                onFailure: _ => -1
            );

            var failureValue = failureResult.Match(
                onSuccess: () => 42,
                onFailure: _ => -1
            );

            // Assert
            successValue.Should().Be(42);
            failureValue.Should().Be(-1);
        }

        [Fact]
        public void Match_WithComplexReturnType_WorksCorrectly()
        {
            // Arrange
            var successResult = Result.Success();
            var failureResult = Result.Failure(new ArgumentException("Invalid"));

            // Act
            var successObject = successResult.Match(
                onSuccess: () => new { Status = "OK", Code = 200 },
                onFailure: ex => new { Status = ex.GetType().Name, Code = 500 }
            );

            var failureObject = failureResult.Match(
                onSuccess: () => new { Status = "OK", Code = 200 },
                onFailure: ex => new { Status = ex.GetType().Name, Code = 500 }
            );

            // Assert
            successObject.Status.Should().Be("OK");
            successObject.Code.Should().Be(200);
            failureObject.Status.Should().Be("ArgumentException");
            failureObject.Code.Should().Be(500);
        }

        #endregion

        #region Fluent Chaining Tests

        [Fact]
        public void FluentChaining_OnSuccessAndOnFailure_WorksCorrectly()
        {
            // Arrange
            var successResult = Result.Success();
            var failureResult = Result.Failure(new Exception("Error"));
            var successExecuted = false;
            var failureExecuted = false;

            // Act - Success case
            successResult
                .OnSuccess(() => successExecuted = true)
                .OnFailure(ex => failureExecuted = true);

            // Assert
            successExecuted.Should().BeTrue();
            failureExecuted.Should().BeFalse();

            // Reset
            successExecuted = false;
            failureExecuted = false;

            // Act - Failure case
            failureResult
                .OnSuccess(() => successExecuted = true)
                .OnFailure(ex => failureExecuted = true);

            // Assert
            successExecuted.Should().BeFalse();
            failureExecuted.Should().BeTrue();
        }

        [Fact]
        public void FluentChaining_MultipleOnSuccess_ExecutesAllOnSuccess()
        {
            // Arrange
            var result = Result.Success();
            var executionOrder = new List<int>();

            // Act
            result
                .OnSuccess(() => executionOrder.Add(1))
                .OnSuccess(() => executionOrder.Add(2))
                .OnSuccess(() => executionOrder.Add(3));

            // Assert
            executionOrder.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        }

        #endregion

        #region Exception Type Tests

        [Theory]
        [InlineData(typeof(ArgumentNullException), "Value cannot be null")]
        [InlineData(typeof(InvalidOperationException), "Invalid operation")]
        [InlineData(typeof(NotSupportedException), "Not supported")]
        [InlineData(typeof(TimeoutException), "Operation timed out")]
        public void DifferentExceptionTypes_HandleCorrectly(Type exceptionType, string message)
        {
            // Arrange
            var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;

            // Act
            var result = Result.Failure(exception);

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
            var result1 = Result.Success();
            var result2 = Result.Success();

            // Act & Assert
            result1.Should().Be(result2); // Value equality
            result1.GetHashCode().Should().Be(result2.GetHashCode());
        }

        [Fact]
        public void Result_AsStruct_CopiesCorrectly()
        {
            // Arrange
            var original = Result.Success();

            // Act
            var copy = original;
            var afterOnSuccess = copy.OnSuccess(() => { /* no-op */ });

            // Assert
            copy.IsSuccess.Should().Be(original.IsSuccess);
            afterOnSuccess.IsSuccess.Should().Be(original.IsSuccess);
        }

        [Fact]
        public void Result_FailureResults_WithSameException_AreEqual()
        {
            // Arrange
            var exception = new Exception("Test");
            var result1 = Result.Failure(exception);
            var result2 = Result.Failure(exception);

            // Act & Assert
            result1.Should().Be(result2);
            result1.Exception.Should().BeSameAs(result2.Exception);
        }

        #endregion

        #region Complex Scenarios Tests

        [Fact]
        public void ComplexScenario_ConditionalExecutionFlow()
        {
            // Arrange
            var executionLog = new List<string>();
            var shouldFail = false;

            // Helper function that might fail
            Result PerformOperation()
            {
                executionLog.Add("Performing operation");
                return shouldFail 
                    ? Result.Failure(new Exception("Operation failed")) 
                    : Result.Success();
            }

            // Act - Success path
            var successResult = PerformOperation()
                .OnSuccess(() => executionLog.Add("Operation succeeded"))
                .OnFailure(ex => executionLog.Add($"Operation failed: {ex.Message}"))
                .OnSuccess(() => executionLog.Add("Continuing with next step"));

            // Assert success path
            executionLog.Should().BeEquivalentTo(new[]
            {
                "Performing operation",
                "Operation succeeded",
                "Continuing with next step"
            });

            // Reset for failure path
            executionLog.Clear();
            shouldFail = true;

            // Act - Failure path
            var failureResult = PerformOperation()
                .OnSuccess(() => executionLog.Add("Operation succeeded"))
                .OnFailure(ex => executionLog.Add($"Operation failed: {ex.Message}"))
                .OnSuccess(() => executionLog.Add("Continuing with next step"));

            // Assert failure path
            executionLog.Should().BeEquivalentTo(new[]
            {
                "Performing operation",
                "Operation failed: Operation failed"
            });
        }

        [Fact]
        public void ComplexScenario_ErrorHandlingWithRecovery()
        {
            // Arrange
            var attempts = 0;
            var maxAttempts = 3;
            var results = new List<Result>();

            // Act
            while (attempts < maxAttempts)
            {
                attempts++;
                var result = attempts < 3
                    ? Result.Failure(new Exception($"Attempt {attempts} failed"))
                    : Result.Success();

                result
                    .OnFailure(ex => Console.WriteLine($"Retry needed: {ex.Message}"))
                    .OnSuccess(() => Console.WriteLine("Success!"));

                results.Add(result);

                if (result.IsSuccess)
                    break;
            }

            // Assert
            results.Should().HaveCount(3);
            results[0].IsFailure.Should().BeTrue();
            results[1].IsFailure.Should().BeTrue();
            results[2].IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void ComplexScenario_AggregatingMultipleResults()
        {
            // Arrange
            var operations = new List<Func<Result>>
            {
                () => Result.Success(),
                () => Result.Success(),
                () => Result.Failure(new Exception("Operation 3 failed")),
                () => Result.Success()
            };

            // Act
            var allSuccessful = true;
            var errors = new List<Exception>();

            foreach (var operation in operations)
            {
                var result = operation();
                result
                    .OnFailure(ex =>
                    {
                        allSuccessful = false;
                        errors.Add(ex);
                    });
            }

            // Assert
            allSuccessful.Should().BeFalse();
            errors.Should().HaveCount(1);
            errors[0].Message.Should().Be("Operation 3 failed");
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void ExceptionWithInnerException_PreservesFullExceptionChain()
        {
            // Arrange
            var innerException = new ArgumentException("Inner error");
            var outerException = new InvalidOperationException("Outer error", innerException);

            // Act
            var result = Result.Failure(outerException);

            // Assert
            result.Exception.Should().Be(outerException);
            result.Exception.InnerException.Should().Be(innerException);
            result.Exception.Message.Should().Be("Outer error");
            result.Exception.InnerException!.Message.Should().Be("Inner error");
        }

        [Fact]
        public void CustomException_WithAdditionalProperties_PreservesAllData()
        {
            // Arrange
            var customException = new CustomTestException
            {
                ErrorCode = "TEST_001",
                Timestamp = DateTime.UtcNow
            };

            // Act
            var result = Result.Failure(customException);

            // Assert
            result.IsFailure.Should().BeTrue();
            var retrievedException = result.Exception as CustomTestException;
            retrievedException.Should().NotBeNull();
            retrievedException!.ErrorCode.Should().Be("TEST_001");
            retrievedException.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        #endregion

        // Helper class for testing
        private class CustomTestException : Exception
        {
            public string ErrorCode { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }

            public CustomTestException() : base("Custom test exception")
            {
            }
        }
    }
}