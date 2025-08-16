using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Unit tests for ExponentialBackoffRetryPolicy with data-driven test patterns.
    /// </summary>
    public class ExponentialBackoffRetryPolicyTests
    {
        private readonly Mock<ILogger<ExponentialBackoffRetryPolicy>> _mockLogger;
        private readonly Mock<IFailureClassifier> _mockClassifier;
        private readonly ExponentialBackoffRetryPolicy _retryPolicy;

        public ExponentialBackoffRetryPolicyTests()
        {
            _mockLogger = new Mock<ILogger<ExponentialBackoffRetryPolicy>>();
            _mockClassifier = new Mock<IFailureClassifier>();
            _retryPolicy = new ExponentialBackoffRetryPolicy(
                _mockLogger.Object,
                _mockClassifier.Object,
                maxAttempts: 3,
                initialDelayMs: 100,
                maxDelayMs: 1000,
                backoffMultiplier: 2.0);
        }

        [Theory]
        [InlineData(1, 100, 200, "First attempt should be near initial delay")]
        [InlineData(2, 200, 400, "Second attempt should be doubled")]
        [InlineData(3, 400, 800, "Third attempt should be quadrupled")]
        [InlineData(4, 800, 1000, "Fourth attempt should be capped at max delay")]
        [InlineData(5, 1000, 1000, "Fifth attempt should remain at max delay")]
        public void CalculateDelay_ProducesCorrectBackoff(
            int attemptNumber, int minExpected, int maxExpected, string scenario)
        {
            // Act
            var delay = _retryPolicy.CalculateDelay(attemptNumber);

            // Assert
            Assert.InRange(delay, minExpected * 0.5, maxExpected * 1.5); // Allow for jitter
        }

        [Theory]
        [InlineData(FailureType.Transient, true, "Transient failures should be retried")]
        [InlineData(FailureType.Permanent, false, "Permanent failures should not be retried")]
        [InlineData(FailureType.Unknown, true, "Unknown failures should be retried by default")]
        public void ShouldRetry_ClassifiesFailuresCorrectly(
            FailureType failureType, bool expectedRetry, string scenario)
        {
            // Arrange
            var exception = new Exception("Test exception");
            _mockClassifier.Setup(c => c.Classify(exception)).Returns(failureType);

            // Act
            var shouldRetry = _retryPolicy.ShouldRetry(exception);

            // Assert
            Assert.Equal(expectedRetry, shouldRetry);
        }

        [Fact]
        public async Task ExecuteAsync_SucceedsOnFirstAttempt()
        {
            // Arrange
            var expectedResult = "Success";
            var operation = new Mock<Func<CancellationToken, Task<string>>>();
            operation.Setup(o => o(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedResult);

            // Act
            var result = await _retryPolicy.ExecuteAsync(operation.Object);

            // Assert
            Assert.Equal(expectedResult, result);
            operation.Verify(o => o(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_RetriesOnTransientFailure()
        {
            // Arrange
            var expectedResult = "Success";
            var attempts = 0;
            var operation = new Mock<Func<CancellationToken, Task<string>>>();
            
            operation.Setup(o => o(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(() =>
                     {
                         attempts++;
                         if (attempts < 3)
                             throw new HttpRequestException("Network error");
                         return expectedResult;
                     });

            _mockClassifier.Setup(c => c.Classify(It.IsAny<HttpRequestException>()))
                          .Returns(FailureType.Transient);

            // Act
            var result = await _retryPolicy.ExecuteAsync(operation.Object);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ExecuteAsync_ThrowsOnPermanentFailure()
        {
            // Arrange
            var permanentException = new ArgumentException("Invalid argument");
            var operation = new Mock<Func<CancellationToken, Task<string>>>();
            operation.Setup(o => o(It.IsAny<CancellationToken>()))
                     .ThrowsAsync(permanentException);

            _mockClassifier.Setup(c => c.Classify(permanentException))
                          .Returns(FailureType.Permanent);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _retryPolicy.ExecuteAsync(operation.Object));
            
            operation.Verify(o => o(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ThrowsAggregateExceptionAfterMaxAttempts()
        {
            // Arrange
            var operation = new Mock<Func<CancellationToken, Task<string>>>();
            operation.Setup(o => o(It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new HttpRequestException("Network error"));

            _mockClassifier.Setup(c => c.Classify(It.IsAny<HttpRequestException>()))
                          .Returns(FailureType.Transient);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AggregateException>(
                () => _retryPolicy.ExecuteAsync(operation.Object));
            
            Assert.Equal(3, exception.InnerExceptions.Count);
            operation.Verify(o => o(It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteWithCallbackAsync_InvokesCallbackOnRetry()
        {
            // Arrange
            var callbackInvocations = new List<(int attempt, Exception exception, int delay)>();
            var operation = new Mock<Func<CancellationToken, Task<string>>>();
            var attempts = 0;
            
            operation.Setup(o => o(It.IsAny<CancellationToken>()))
                     .ReturnsAsync(() =>
                     {
                         attempts++;
                         if (attempts < 3)
                             throw new HttpRequestException($"Attempt {attempts}");
                         return "Success";
                     });

            _mockClassifier.Setup(c => c.Classify(It.IsAny<HttpRequestException>()))
                          .Returns(FailureType.Transient);

            // Act
            var result = await _retryPolicy.ExecuteWithCallbackAsync(
                operation.Object,
                (attempt, ex, delay) => callbackInvocations.Add((attempt, ex, delay)));

            // Assert
            Assert.Equal("Success", result);
            Assert.Equal(2, callbackInvocations.Count);
            Assert.Equal(1, callbackInvocations[0].attempt);
            Assert.Equal(2, callbackInvocations[1].attempt);
            Assert.Contains("Attempt 1", callbackInvocations[0].exception.Message);
            Assert.Contains("Attempt 2", callbackInvocations[1].exception.Message);
        }

        [Theory]
        [InlineData(0, typeof(ArgumentException), "Zero max attempts should throw")]
        [InlineData(-1, typeof(ArgumentException), "Negative max attempts should throw")]
        [InlineData(1, null, "One max attempt is valid")]
        [InlineData(10, null, "Ten max attempts is valid")]
        public void Constructor_ValidatesMaxAttempts(
            int maxAttempts, Type expectedExceptionType, string scenario)
        {
            // Act & Assert
            if (expectedExceptionType != null)
            {
                Assert.Throws(expectedExceptionType, () =>
                    new ExponentialBackoffRetryPolicy(
                        _mockLogger.Object,
                        _mockClassifier.Object,
                        maxAttempts: maxAttempts));
            }
            else
            {
                var policy = new ExponentialBackoffRetryPolicy(
                    _mockLogger.Object,
                    _mockClassifier.Object,
                    maxAttempts: maxAttempts);
                Assert.Equal(maxAttempts, policy.MaxAttempts);
            }
        }

        [Fact]
        public async Task ExecuteAsync_RespectsCancel()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var operation = new Mock<Func<CancellationToken, Task<string>>>();
            
            operation.Setup(o => o(It.IsAny<CancellationToken>()))
                     .Returns<CancellationToken>(async ct =>
                     {
                         await Task.Delay(100, ct);
                         ct.ThrowIfCancellationRequested();
                         return "Success";
                     });

            // Act
            cts.Cancel();
            
            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _retryPolicy.ExecuteAsync(operation.Object, cts.Token));
        }
    }

    /// <summary>
    /// Unit tests for OCRFailureClassifier with data-driven test patterns.
    /// </summary>
    public class OCRFailureClassifierTests
    {
        private readonly Mock<ILogger<OCRFailureClassifier>> _mockLogger;
        private readonly OCRFailureClassifier _classifier;

        public OCRFailureClassifierTests()
        {
            _mockLogger = new Mock<ILogger<OCRFailureClassifier>>();
            _classifier = new OCRFailureClassifier(_mockLogger.Object);
        }

        public static IEnumerable<object[]> HttpExceptionTestData =>
            new List<object[]>
            {
                new object[] { "429 Too Many Requests", FailureType.Transient, "Rate limiting" },
                new object[] { "503 Service Unavailable", FailureType.Transient, "Service down" },
                new object[] { "504 Gateway Timeout", FailureType.Transient, "Gateway timeout" },
                new object[] { "400 Bad Request", FailureType.Permanent, "Bad request" },
                new object[] { "401 Unauthorized", FailureType.Permanent, "Auth failure" },
                new object[] { "404 Not Found", FailureType.Permanent, "Not found" },
                new object[] { "Network connection failed", FailureType.Transient, "Network error" },
                new object[] { "Connection timeout", FailureType.Transient, "Timeout" },
                new object[] { "Unknown error", FailureType.Unknown, "Unknown error" }
            };

        [Theory]
        [MemberData(nameof(HttpExceptionTestData))]
        public void Classify_HttpExceptions_ReturnsCorrectType(
            string errorMessage, FailureType expectedType, string scenario)
        {
            // Arrange
            var exception = new HttpRequestException(errorMessage);

            // Act
            var result = _classifier.Classify(exception);

            // Assert
            Assert.Equal(expectedType, result);
        }

        [Theory]
        [InlineData(typeof(TimeoutException), FailureType.Transient, "Timeout exceptions are transient")]
        [InlineData(typeof(TaskCanceledException), FailureType.Transient, "Cancelled tasks are transient")]
        [InlineData(typeof(ArgumentException), FailureType.Permanent, "Argument exceptions are permanent")]
        [InlineData(typeof(InvalidOperationException), FailureType.Permanent, "Invalid operations are permanent")]
        [InlineData(typeof(NotImplementedException), FailureType.Unknown, "Unknown exceptions default to unknown")]
        public void Classify_ExceptionTypes_ReturnsCorrectType(
            Type exceptionType, FailureType expectedType, string scenario)
        {
            // Arrange
            var exception = (Exception)Activator.CreateInstance(exceptionType, "Test message");

            // Act
            var result = _classifier.Classify(exception);

            // Assert
            Assert.Equal(expectedType, result);
        }

        [Fact]
        public void Classify_NullException_ReturnsUnknown()
        {
            // Act
            var result = _classifier.Classify(null);

            // Assert
            Assert.Equal(FailureType.Unknown, result);
        }

        [Fact]
        public void Classify_OCRServiceException_WithInnerException()
        {
            // Arrange
            var innerException = new HttpRequestException("503 Service Unavailable");
            var ocrException = new OCRServiceException("OCR failed", innerException);

            // Act
            var result = _classifier.Classify(ocrException);

            // Assert
            Assert.Equal(FailureType.Transient, result);
        }

        [Theory]
        [InlineData("Invalid document format", FailureType.Permanent, "Invalid format")]
        [InlineData("Unsupported file type", FailureType.Permanent, "Unsupported type")]
        [InlineData("Required field missing", FailureType.Permanent, "Missing field")]
        [InlineData("Service timeout occurred", FailureType.Transient, "Timeout")]
        [InlineData("Service unavailable", FailureType.Transient, "Unavailable")]
        [InlineData("Server is busy", FailureType.Transient, "Busy server")]
        [InlineData("Unknown processing error", FailureType.Unknown, "Unknown error")]
        public void Classify_OCRServiceException_ByMessage(
            string errorMessage, FailureType expectedType, string scenario)
        {
            // Arrange
            var exception = new OCRServiceException(errorMessage);

            // Act
            var result = _classifier.Classify(exception);

            // Assert
            Assert.Equal(expectedType, result);
        }
    }
}