using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Common.Utilities;
using Xunit;

namespace NoLock.Social.Core.Tests.Common.Utilities
{
    public class ExceptionHandlerTests
    {
        private readonly Mock<ILogger> _loggerMock;

        public ExceptionHandlerTests()
        {
            _loggerMock = new Mock<ILogger>();
        }

        [Theory]
        [InlineData("Operation1", 42, true)]
        [InlineData("Operation2", 100, false)]
        [InlineData("LongOperation", 999, true)]
        [InlineData("QuickOp", -1, false)]
        [InlineData("ComplexOperation", 0, true)]
        public async Task ExecuteAsync_WithReturnValue_Success(string operationName, int expectedValue, bool logDebug)
        {
            // Arrange
            var operation = new Func<Task<int>>(async () =>
            {
                await Task.Delay(10);
                return expectedValue;
            });

            // Act
            var result = await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                operation,
                operationName);

            // Assert
            result.Should().Be(expectedValue);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Theory]
        [InlineData("DatabaseError", "Connection timeout", true)]
        [InlineData("NetworkError", "Host unreachable", false)]
        [InlineData("ValidationError", "Invalid input", true)]
        [InlineData("AuthError", "Unauthorized access", false)]
        public async Task ExecuteAsync_WithReturnValue_ExceptionHandling(string operationName, string errorMessage, bool rethrow)
        {
            // Arrange
            var exception = new InvalidOperationException(errorMessage);
            var operation = new Func<Task<int>>(async () =>
            {
                await Task.Delay(10);
                throw exception;
            });

            // Act & Assert
            if (rethrow)
            {
                var act = async () => await ExceptionHandler.ExecuteAsync(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage(errorMessage);
            }
            else
            {
                var result = await ExceptionHandler.ExecuteAsync(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);

                result.Should().Be(default(int));
            }

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operationName)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("SyncOp1", "test value 1", true)]
        [InlineData("SyncOp2", "test value 2", false)]
        [InlineData("SyncOp3", null, true)]
        [InlineData("SyncOp4", "", false)]
        public void Execute_WithReturnValue_Success(string operationName, string expectedValue, bool logDebug)
        {
            // Arrange
            var operation = new Func<string>(() => expectedValue);

            // Act
            var result = ExceptionHandler.Execute(
                _loggerMock.Object,
                operation,
                operationName);

            // Assert
            result.Should().Be(expectedValue);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Theory]
        [InlineData("FileError", "File not found", true)]
        [InlineData("ParseError", "Invalid format", false)]
        [InlineData("ConfigError", "Missing configuration", true)]
        [InlineData("RuntimeError", "Runtime exception", false)]
        public void Execute_WithReturnValue_ExceptionHandling(string operationName, string errorMessage, bool rethrow)
        {
            // Arrange
            var exception = new InvalidOperationException(errorMessage);
            var operation = new Func<string>(() => throw exception);

            // Act & Assert
            if (rethrow)
            {
                var act = () => ExceptionHandler.Execute(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);

                act.Should().Throw<InvalidOperationException>()
                    .WithMessage(errorMessage);
            }
            else
            {
                var result = ExceptionHandler.Execute(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);

                result.Should().Be(default(string));
            }

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operationName)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("VoidAsyncOp1", 10)]
        [InlineData("VoidAsyncOp2", 50)]
        [InlineData("VoidAsyncOp3", 0)]
        [InlineData("VoidAsyncOp4", 100)]
        public async Task ExecuteAsync_Void_Success(string operationName, int delayMs)
        {
            // Arrange
            var executed = false;
            var operation = new Func<Task>(async () =>
            {
                await Task.Delay(delayMs);
                executed = true;
            });

            // Act
            await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                operation,
                operationName);

            // Assert
            executed.Should().BeTrue();
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Theory]
        [InlineData("AsyncVoidError1", "Async operation failed", true)]
        [InlineData("AsyncVoidError2", "Task cancelled", false)]
        [InlineData("AsyncVoidError3", "Timeout occurred", true)]
        [InlineData("AsyncVoidError4", "Resource unavailable", false)]
        public async Task ExecuteAsync_Void_ExceptionHandling(string operationName, string errorMessage, bool rethrow)
        {
            // Arrange
            var exception = new InvalidOperationException(errorMessage);
            var operation = new Func<Task>(async () =>
            {
                await Task.Delay(10);
                throw exception;
            });

            // Act & Assert
            if (rethrow)
            {
                var act = async () => await ExceptionHandler.ExecuteAsync(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage(errorMessage);
            }
            else
            {
                await ExceptionHandler.ExecuteAsync(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);
            }

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operationName)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("VoidOp1")]
        [InlineData("VoidOp2")]
        [InlineData("VoidOp3")]
        [InlineData("VoidOp4")]
        public void Execute_Void_Success(string operationName)
        {
            // Arrange
            var executed = false;
            var operation = new Action(() => executed = true);

            // Act
            ExceptionHandler.Execute(
                _loggerMock.Object,
                operation,
                operationName);

            // Assert
            executed.Should().BeTrue();
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Theory]
        [InlineData("VoidError1", "Operation failed", true)]
        [InlineData("VoidError2", "Invalid state", false)]
        [InlineData("VoidError3", "Access denied", true)]
        [InlineData("VoidError4", "Resource locked", false)]
        public void Execute_Void_ExceptionHandling(string operationName, string errorMessage, bool rethrow)
        {
            // Arrange
            var exception = new InvalidOperationException(errorMessage);
            var operation = new Action(() => throw exception);

            // Act & Assert
            if (rethrow)
            {
                var act = () => ExceptionHandler.Execute(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);

                act.Should().Throw<InvalidOperationException>()
                    .WithMessage(errorMessage);
            }
            else
            {
                ExceptionHandler.Execute(
                    _loggerMock.Object,
                    operation,
                    operationName,
                    rethrow);
            }

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operationName)),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithCancellationToken_ShouldHandleCancellation()
        {
            // Arrange
            var cts = new System.Threading.CancellationTokenSource();
            var operation = new Func<Task<int>>(async () =>
            {
                await Task.Delay(1000, cts.Token);
                return 42;
            });

            // Act
            cts.Cancel();
            var act = async () => await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                operation,
                "CancellationTest");

            // Assert
            await act.Should().ThrowAsync<TaskCanceledException>();
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CancellationTest")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(typeof(ArgumentNullException), "Value cannot be null")]
        [InlineData(typeof(ArgumentException), "Invalid argument")]
        [InlineData(typeof(InvalidOperationException), "Invalid operation")]
        [InlineData(typeof(NotSupportedException), "Not supported")]
        [InlineData(typeof(TimeoutException), "Operation timed out")]
        public async Task ExecuteAsync_DifferentExceptionTypes(Type exceptionType, string message)
        {
            // Arrange
            var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;
            var operation = new Func<Task<string>>(async () =>
            {
                await Task.Delay(10);
                throw exception;
            });

            // Act
            var act = async () => await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                operation,
                $"Test_{exceptionType.Name}");

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .Where(e => e.GetType() == exceptionType);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Test_{exceptionType.Name}")),
                It.Is<Exception>(e => e.GetType() == exceptionType),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NestedExceptions_ShouldLogCorrectly()
        {
            // Arrange
            var innerException = new ArgumentException("Inner exception");
            var outerException = new InvalidOperationException("Outer exception", innerException);
            var operation = new Func<Task<int>>(async () =>
            {
                await Task.Delay(10);
                throw outerException;
            });

            // Act
            var act = async () => await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                operation,
                "NestedExceptionTest");

            // Assert
            var exception = await act.Should().ThrowAsync<InvalidOperationException>();
            exception.WithMessage("Outer exception");
            exception.Which.InnerException.Should().BeOfType<ArgumentException>();
            exception.Which.InnerException!.Message.Should().Be("Inner exception");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NestedExceptionTest")),
                It.Is<Exception>(e => e == outerException && e.InnerException == innerException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}