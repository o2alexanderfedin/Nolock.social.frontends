using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.Common.Utilities;
using Xunit;

namespace NoLock.Social.Core.Tests.Common.Utilities
{
    public class ExceptionHandlerTests
    {
        private readonly Mock<ILogger<ExceptionHandlerTests>> _loggerMock;

        public ExceptionHandlerTests()
        {
            _loggerMock = new Mock<ILogger<ExceptionHandlerTests>>();
        }

        #region ExecuteAsync<T> Tests

        [Fact]
        public async Task ExecuteAsync_WithResult_ShouldReturnResult_WhenOperationSucceeds()
        {
            // Arrange
            var expectedResult = "test-result";

            // Act
            var result = await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => await Task.FromResult(expectedResult),
                "TestOperation"
            );

            // Assert
            result.Should().Be(expectedResult);
            _loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithResult_ShouldLogErrorAndRethrow_WhenOperationFailsAndRethrowTrue()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var act = async () => await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => { await Task.Delay(1); throw exception; },
                "TestOperation",
                rethrow: true
            );

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Test error");
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithResult_ShouldLogErrorAndReturnDefault_WhenOperationFailsAndRethrowFalse()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var result = await ExceptionHandler.ExecuteAsync<string>(
                _loggerMock.Object,
                async () => { await Task.Delay(1); throw exception; },
                "TestOperation",
                rethrow: false
            );

            // Assert
            result.Should().BeNull();
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExecuteAsync_WithResult_ShouldHandleCancellationTokenException(bool rethrow)
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            if (rethrow)
            {
                var act = async () => await ExceptionHandler.ExecuteAsync(
                    _loggerMock.Object,
                    async () => { await Task.Delay(1000, cts.Token); return "result"; },
                    "CancelledOperation",
                    rethrow: true
                );

                await act.Should().ThrowAsync<OperationCanceledException>();
            }
            else
            {
                var result = await ExceptionHandler.ExecuteAsync<string>(
                    _loggerMock.Object,
                    async () => { await Task.Delay(1000, cts.Token); return "result"; },
                    "CancelledOperation",
                    rethrow: false
                );

                result.Should().BeNull();
            }
        }

        #endregion

        #region Execute<T> Tests

        [Fact]
        public void Execute_WithResult_ShouldReturnResult_WhenOperationSucceeds()
        {
            // Arrange
            var expectedResult = 42;

            // Act
            var result = ExceptionHandler.Execute(
                _loggerMock.Object,
                () => expectedResult,
                "TestOperation"
            );

            // Assert
            result.Should().Be(expectedResult);
            _loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void Execute_WithResult_ShouldLogErrorAndRethrow_WhenOperationFailsAndRethrowTrue()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var act = () => ExceptionHandler.Execute(
                _loggerMock.Object,
                () => throw exception,
                "TestOperation",
                rethrow: true
            );

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Test error");
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Execute_WithResult_ShouldLogErrorAndReturnDefault_WhenOperationFailsAndRethrowFalse()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var result = ExceptionHandler.Execute<int>(
                _loggerMock.Object,
                () => throw exception,
                "TestOperation",
                rethrow: false
            );

            // Assert
            result.Should().Be(0);
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region ExecuteAsync (void) Tests

        [Fact]
        public async Task ExecuteAsync_Void_ShouldCompleteSuccessfully_WhenOperationSucceeds()
        {
            // Arrange
            var executed = false;

            // Act
            await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => { await Task.Delay(1); executed = true; },
                "TestOperation"
            );

            // Assert
            executed.Should().BeTrue();
            _loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_Void_ShouldLogErrorAndRethrow_WhenOperationFailsAndRethrowTrue()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var act = async () => await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => { await Task.Delay(1); throw exception; },
                "TestOperation",
                rethrow: true
            );

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Test error");
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_Void_ShouldLogErrorAndContinue_WhenOperationFailsAndRethrowFalse()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");
            var executed = false;

            // Act
            await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => { await Task.Delay(1); throw exception; },
                "TestOperation",
                rethrow: false
            );
            
            executed = true; // This should execute even after exception

            // Assert
            executed.Should().BeTrue();
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Execute (void) Tests

        [Fact]
        public void Execute_Void_ShouldCompleteSuccessfully_WhenOperationSucceeds()
        {
            // Arrange
            var executed = false;

            // Act
            ExceptionHandler.Execute(
                _loggerMock.Object,
                () => { executed = true; },
                "TestOperation"
            );

            // Assert
            executed.Should().BeTrue();
            _loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void Execute_Void_ShouldLogErrorAndRethrow_WhenOperationFailsAndRethrowTrue()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var act = () => ExceptionHandler.Execute(
                _loggerMock.Object,
                () => throw exception,
                "TestOperation",
                rethrow: true
            );

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Test error");
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void Execute_Void_ShouldLogErrorAndContinue_WhenOperationFailsAndRethrowFalse()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");
            var executed = false;

            // Act
            ExceptionHandler.Execute(
                _loggerMock.Object,
                () => throw exception,
                "TestOperation",
                rethrow: false
            );
            
            executed = true; // This should execute even after exception

            // Assert
            executed.Should().BeTrue();
            
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TestOperation failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Edge Cases and Special Scenarios

        [Theory]
        [InlineData("Operation1")]
        [InlineData("Complex Operation with Spaces")]
        [InlineData("Operation-with-dashes")]
        [InlineData("Operation_with_underscores")]
        public async Task ExecuteAsync_ShouldLogCorrectOperationName(string operationName)
        {
            // Arrange
            var exception = new Exception("Test");

            // Act
            await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => { await Task.Delay(1); throw exception; },
                operationName,
                rethrow: false
            );

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"{operationName} failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldHandleNestedExceptions()
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner error");
            var outerException = new ApplicationException("Outer error", innerException);

            // Act
            var act = async () => await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => { await Task.Delay(1); throw outerException; },
                "NestedExceptionOperation",
                rethrow: true
            );

            // Assert
            var exception = await act.Should().ThrowAsync<ApplicationException>()
                .WithMessage("Outer error");
            exception.And.InnerException.Should().BeOfType<InvalidOperationException>();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldHandleMultipleConsecutiveCalls()
        {
            // Arrange & Act
            var result1 = await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => await Task.FromResult(1),
                "Operation1"
            );

            var result2 = await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => await Task.FromResult(2),
                "Operation2"
            );

            var result3 = await ExceptionHandler.ExecuteAsync(
                _loggerMock.Object,
                async () => await Task.FromResult(3),
                "Operation3"
            );

            // Assert
            result1.Should().Be(1);
            result2.Should().Be(2);
            result3.Should().Be(3);
            _loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        #endregion
    }
}