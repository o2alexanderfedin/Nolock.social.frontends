using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Common.Extensions;
using NoLock.Social.Core.Common.Results;

namespace NoLock.Social.Core.Tests.Common.Extensions
{
    public class ResultExtensionsTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public ResultExtensionsTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        #region ExecuteWithLogging<T> Async Tests

        [Fact]
        public async Task ExecuteWithLoggingAsync_Generic_WhenOperationSucceeds_ReturnsSuccessResult()
        {
            // Arrange
            var expectedValue = "test value";
            Func<Task<string>> operation = async () =>
            {
                await Task.Delay(1);
                return expectedValue;
            };

            // Act
            var result = await _mockLogger.Object.ExecuteWithLogging(operation, "TestOperation");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(expectedValue);

            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteWithLoggingAsync_Generic_WhenOperationFails_ReturnsFailureResultAndLogs()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");
            Func<Task<string>> operation = async () =>
            {
                await Task.Delay(1);
                throw exception;
            };

            // Act
            var result = await _mockLogger.Object.ExecuteWithLogging(operation, "TestOperation");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().Be(exception);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("TestOperation failed")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteWithLoggingAsync_Generic_WithNullReturn_HandlesCorrectly()
        {
            // Arrange
            string? nullValue = null;
            Func<Task<string?>> operation = async () =>
            {
                await Task.Delay(1);
                return nullValue;
            };

            // Act
            var result = await _mockLogger.Object.ExecuteWithLogging(operation, "NullOperation");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeNull();
        }

        #endregion

        #region ExecuteWithLogging<T> Sync Tests

        [Fact]
        public void ExecuteWithLoggingSync_Generic_WhenOperationSucceeds_ReturnsSuccessResult()
        {
            // Arrange
            var expectedValue = 42;
            Func<int> operation = () => expectedValue;

            // Act
            var result = _mockLogger.Object.ExecuteWithLogging(operation, "TestOperation");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(expectedValue);

            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void ExecuteWithLoggingSync_Generic_WhenOperationFails_ReturnsFailureResultAndLogs()
        {
            // Arrange
            var exception = new ArgumentException("Test error");
            Func<int> operation = () => throw exception;

            // Act
            var result = _mockLogger.Object.ExecuteWithLogging(operation, "FailingOperation");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().Be(exception);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("FailingOperation failed")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ExecuteWithLoggingSync_Generic_WithComplexObject_ReturnsCorrectly()
        {
            // Arrange
            var complexObject = new TestObject { Id = 1, Name = "Test", Values = new[] { 1, 2, 3 } };
            Func<TestObject> operation = () => complexObject;

            // Act
            var result = _mockLogger.Object.ExecuteWithLogging(operation, "ComplexOperation");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeSameAs(complexObject);
            result.Value.Id.Should().Be(1);
            result.Value.Name.Should().Be("Test");
            result.Value.Values.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        }

        #endregion

        #region ExecuteWithLogging Non-Generic Async Tests

        [Fact]
        public async Task ExecuteWithLoggingAsync_NonGeneric_WhenOperationSucceeds_ReturnsSuccessResult()
        {
            // Arrange
            var wasExecuted = false;
            Func<Task> operation = async () =>
            {
                await Task.Delay(1);
                wasExecuted = true;
            };

            // Act
            var result = await _mockLogger.Object.ExecuteWithLogging(operation, "VoidOperation");

            // Assert
            result.IsSuccess.Should().BeTrue();
            wasExecuted.Should().BeTrue();

            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteWithLoggingAsync_NonGeneric_WhenOperationFails_ReturnsFailureResultAndLogs()
        {
            // Arrange
            var exception = new NotSupportedException("Not supported");
            Func<Task> operation = async () =>
            {
                await Task.Delay(1);
                throw exception;
            };

            // Act
            var result = await _mockLogger.Object.ExecuteWithLogging(operation, "FailingVoidOperation");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().Be(exception);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("FailingVoidOperation failed")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region ExecuteWithLogging Non-Generic Sync Tests

        [Fact]
        public void ExecuteWithLoggingSync_NonGeneric_WhenOperationSucceeds_ReturnsSuccessResult()
        {
            // Arrange
            var wasExecuted = false;
            Action operation = () => wasExecuted = true;

            // Act
            var result = _mockLogger.Object.ExecuteWithLogging(operation, "ActionOperation");

            // Assert
            result.IsSuccess.Should().BeTrue();
            wasExecuted.Should().BeTrue();

            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void ExecuteWithLoggingSync_NonGeneric_WhenOperationFails_ReturnsFailureResultAndLogs()
        {
            // Arrange
            var exception = new TimeoutException("Timeout occurred");
            Action operation = () => throw exception;

            // Act
            var result = _mockLogger.Object.ExecuteWithLogging(operation, "TimeoutOperation");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Exception.Should().Be(exception);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("TimeoutOperation failed")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ExecuteWithLoggingSync_NonGeneric_WithSideEffects_ExecutesCorrectly()
        {
            // Arrange
            var counter = 0;
            Action operation = () =>
            {
                counter++;
                if (counter > 1)
                    throw new Exception("Should only execute once");
            };

            // Act
            var result = _mockLogger.Object.ExecuteWithLogging(operation, "SideEffectOperation");

            // Assert
            result.IsSuccess.Should().BeTrue();
            counter.Should().Be(1);
        }

        #endregion

        #region ThrowIfFailure Tests

        [Fact]
        public void ThrowIfFailure_Generic_WithSuccessResult_ReturnsValue()
        {
            // Arrange
            var expectedValue = "success";
            var result = Result<string>.Success(expectedValue);

            // Act
            var value = result.ThrowIfFailure();

            // Assert
            value.Should().Be(expectedValue);
        }

        [Fact]
        public void ThrowIfFailure_Generic_WithFailureResult_ThrowsException()
        {
            // Arrange
            var exception = new InvalidOperationException("Operation failed");
            var result = Result<string>.Failure(exception);

            // Act & Assert
            var action = () => result.ThrowIfFailure();
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("Operation failed");
        }

        [Fact]
        public void ThrowIfFailure_NonGeneric_WithSuccessResult_DoesNotThrow()
        {
            // Arrange
            var result = Result.Success();

            // Act & Assert
            var action = () => result.ThrowIfFailure();
            action.Should().NotThrow();
        }

        [Fact]
        public void ThrowIfFailure_NonGeneric_WithFailureResult_ThrowsException()
        {
            // Arrange
            var exception = new ArgumentNullException("param", "Parameter cannot be null");
            var result = Result.Failure(exception);

            // Act & Assert
            var action = () => result.ThrowIfFailure();
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("param")
                .WithMessage("*Parameter cannot be null*");
        }

        #endregion

        #region Complex Scenario Tests

        [Fact]
        public async Task ComplexScenario_ChainedOperationsWithLogging()
        {
            // Arrange
            var executionLog = new List<string>();

            async Task<int> Step1()
            {
                executionLog.Add("Step1 executed");
                await Task.Delay(1);
                return 10;
            }

            int Step2(int input)
            {
                executionLog.Add($"Step2 executed with {input}");
                return input * 2;
            }

            async Task<string> Step3(int input)
            {
                executionLog.Add($"Step3 executed with {input}");
                await Task.Delay(1);
                return $"Result: {input}";
            }

            // Act
            var result1 = await _mockLogger.Object.ExecuteWithLogging(Step1, "Step1");
            var result2 = result1.IsSuccess
                ? _mockLogger.Object.ExecuteWithLogging(() => Step2(result1.Value), "Step2")
                : Result<int>.Failure(result1.Exception);
            var result3 = result2.IsSuccess
                ? await _mockLogger.Object.ExecuteWithLogging(() => Step3(result2.Value), "Step3")
                : Result<string>.Failure(result2.Exception);

            // Assert
            result3.IsSuccess.Should().BeTrue();
            result3.Value.Should().Be("Result: 20");
            executionLog.Should().BeEquivalentTo(new[]
            {
                "Step1 executed",
                "Step2 executed with 10",
                "Step3 executed with 20"
            });
        }

        [Fact]
        public void ComplexScenario_ExceptionPropagationWithLogging()
        {
            // Arrange
            var operations = new List<Func<Result<int>>>
            {
                () => _mockLogger.Object.ExecuteWithLogging(() => 1, "Op1"),
                () => _mockLogger.Object.ExecuteWithLogging(() => 2, "Op2"),
                () => _mockLogger.Object.ExecuteWithLogging(() => { throw new Exception("Op3 failed"); return 0; }, "Op3"),
                () => _mockLogger.Object.ExecuteWithLogging(() => 4, "Op4")
            };

            // Act
            var results = operations.Select(op => op()).ToList();
            var failedOperations = results.Where(r => r.IsFailure).ToList();

            // Assert
            results.Should().HaveCount(4);
            failedOperations.Should().HaveCount(1);
            failedOperations[0].Exception.Message.Should().Be("Op3 failed");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Op3 failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("Operation1", true, null)]
        [InlineData("Operation2", false, "Error in Operation2")]
        [InlineData("LongOperationName_With_Special-Characters.123", true, null)]
        [InlineData("", false, "Empty operation name error")]
        public async Task ParameterizedTests_VariousOperationNames(string operationName, bool shouldSucceed, string? errorMessage)
        {
            // Arrange
            Func<Task<string>> operation = shouldSucceed
                ? (async () =>
                {
                    await Task.Delay(1);
                    return "Success";
                })
                : (async () =>
                {
                    await Task.Delay(1);
                    throw new Exception(errorMessage!);
                });

            // Act
            var result = await _mockLogger.Object.ExecuteWithLogging(operation, operationName);

            // Assert
            if (shouldSucceed)
            {
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be("Success");
            }
            else
            {
                result.IsFailure.Should().BeTrue();
                result.Exception.Message.Should().Be(errorMessage);
            }
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public async Task ExecuteWithLogging_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task<string>> operation = async () =>
            {
                await Task.Delay(1000, cts.Token);
                return "Should not reach here";
            };

            // Act & Assert
            var resultTask = _mockLogger.Object.ExecuteWithLogging(operation, "CancelledOperation");
            var result = await resultTask;

            result.IsFailure.Should().BeTrue();
            result.Exception.Should().BeOfType<TaskCanceledException>();
        }

        [Fact]
        public void ThrowIfFailure_PreservesStackTrace()
        {
            // Arrange
            var originalException = new Exception("Original error");
            var originalStackTrace = string.Empty;

            try
            {
                throw originalException;
            }
            catch (Exception ex)
            {
                originalStackTrace = ex.StackTrace ?? string.Empty;
                var result = Result<string>.Failure(ex);

                // Act & Assert
                var action = () => result.ThrowIfFailure();
                var thrownException = action.Should().Throw<Exception>().Which;

                thrownException.Should().BeSameAs(originalException);
                thrownException.StackTrace.Should().NotBeNullOrEmpty();
            }
        }

        #endregion

        // Helper class for testing
        private class TestObject
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int[] Values { get; set; } = Array.Empty<int>();
        }
    }
}