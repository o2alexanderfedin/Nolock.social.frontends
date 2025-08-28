using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Moq;
using NoLock.Social.Core.Common.Services;
using Xunit;

namespace NoLock.Social.Core.Tests.Common.Services
{
    public class JSRuntimeWrapperTests
    {
        private readonly Mock<IJSRuntime> _mockJSRuntime;
        private readonly JSRuntimeWrapper _wrapper;

        public JSRuntimeWrapperTests()
        {
            _mockJSRuntime = new Mock<IJSRuntime>();
            _wrapper = new JSRuntimeWrapper(_mockJSRuntime.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullJSRuntime_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new JSRuntimeWrapper(null!));
            Assert.Equal("jsRuntime", exception.ParamName);
        }

        [Fact]
        public void Constructor_ValidJSRuntime_CreatesInstance()
        {
            // Arrange
            var mockRuntime = new Mock<IJSRuntime>();

            // Act
            var wrapper = new JSRuntimeWrapper(mockRuntime.Object);

            // Assert
            Assert.NotNull(wrapper);
        }

        #endregion

        #region InvokeAsync<T> Tests

        [Fact]
        public async Task InvokeAsync_WithValidIdentifier_CallsJSRuntime()
        {
            // Arrange
            const string identifier = "testFunction";
            const string expectedResult = "test result";
            var args = new object[] { "arg1", 42 };

            _mockJSRuntime
                .Setup(x => x.InvokeAsync<string>(identifier, args))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _wrapper.InvokeAsync<string>(identifier, args);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>(identifier, args), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithNoArguments_CallsJSRuntime()
        {
            // Arrange
            const string identifier = "noArgsFunction";
            const int expectedResult = 100;

            _mockJSRuntime
                .Setup(x => x.InvokeAsync<int>(identifier, It.IsAny<object[]>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _wrapper.InvokeAsync<int>(identifier);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<int>(identifier, It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithComplexType_ReturnsCorrectType()
        {
            // Arrange
            const string identifier = "getComplexObject";
            var expectedResult = new TestComplexType 
            { 
                Id = 1, 
                Name = "Test",
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            var args = new object[] { 1 };

            _mockJSRuntime
                .Setup(x => x.InvokeAsync<TestComplexType>(identifier, args))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _wrapper.InvokeAsync<TestComplexType>(identifier, args);

            // Assert
            Assert.Equal(expectedResult.Id, result.Id);
            Assert.Equal(expectedResult.Name, result.Name);
            Assert.Equal(expectedResult.IsActive, result.IsActive);
            _mockJSRuntime.Verify(x => x.InvokeAsync<TestComplexType>(identifier, args), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithNullableType_ReturnsNull()
        {
            // Arrange
            const string identifier = "getNullableValue";
            string? expectedResult = null;

            _mockJSRuntime
                .Setup(x => x.InvokeAsync<string?>(identifier, It.IsAny<object[]>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _wrapper.InvokeAsync<string?>(identifier);

            // Assert
            Assert.Null(result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string?>(identifier, It.IsAny<object[]>()), Times.Once);
        }

        [Theory]
        [InlineData("function1", new object[] { "test" }, "result1")]
        [InlineData("function2", new object[] { 123, true }, "result2")]
        [InlineData("function3", new object?[] { null, "test", 456 }, "result3")]
        public async Task InvokeAsync_WithVariousArguments_CallsJSRuntimeCorrectly(
            string identifier, object[] args, string expectedResult)
        {
            // Arrange
            _mockJSRuntime
                .Setup(x => x.InvokeAsync<string>(identifier, args))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _wrapper.InvokeAsync<string>(identifier, args);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>(identifier, args), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WhenJSRuntimeThrows_PropagatesException()
        {
            // Arrange
            const string identifier = "failingFunction";
            var expectedException = new JSException("JavaScript error");

            _mockJSRuntime
                .Setup(x => x.InvokeAsync<string>(identifier, It.IsAny<object[]>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<JSException>(() => 
                _wrapper.InvokeAsync<string>(identifier));
            
            Assert.Equal(expectedException.Message, exception.Message);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>(identifier, It.IsAny<object[]>()), Times.Once);
        }

        #endregion

        #region InvokeVoidAsync Tests

        [Fact]
        public async Task InvokeVoidAsync_WithValidIdentifier_DoesNotThrow()
        {
            // Arrange
            const string identifier = "voidFunction";
            var args = new object[] { "arg1", 42 };
            
            // Since InvokeVoidAsync is an extension method that can't be mocked directly,
            // we simply verify that calling it doesn't throw an exception
            // The actual implementation calls the extension method on IJSRuntime

            // Act & Assert - Should not throw
            await _wrapper.InvokeVoidAsync(identifier, args);
        }

        [Fact]
        public async Task InvokeVoidAsync_WithNoArguments_DoesNotThrow()
        {
            // Arrange
            const string identifier = "voidNoArgsFunction";

            // Act & Assert - Should not throw
            await _wrapper.InvokeVoidAsync(identifier);
        }

        [Theory]
        [InlineData("voidFunc1", new object[] { "test" })]
        [InlineData("voidFunc2", new object[] { 123, true, "test" })]
        [InlineData("voidFunc3", new object?[] { null })]
        [InlineData("voidFunc4", new object[] { })]
        public async Task InvokeVoidAsync_WithVariousArguments_DoesNotThrow(
            string identifier, object[] args)
        {
            // Act & Assert - Should not throw
            await _wrapper.InvokeVoidAsync(identifier, args);
        }

        [Fact]
        public async Task InvokeVoidAsync_WithNullArgument_DoesNotThrow()
        {
            // Arrange
            const string identifier = "handleNull";
            var args = new object?[] { null, "afterNull" };

            // Act & Assert - Should not throw
            await _wrapper.InvokeVoidAsync(identifier, args!);
        }

        #endregion

        #region Concurrent Call Tests

        [Fact]
        public async Task InvokeAsync_ConcurrentCalls_AllCallsSucceed()
        {
            // Arrange
            const string identifier = "concurrentFunction";
            var tasks = new Task<int>[10];

            for (int i = 0; i < 10; i++)
            {
                var index = i;
                _mockJSRuntime
                    .Setup(x => x.InvokeAsync<int>(identifier, new object[] { index }))
                    .ReturnsAsync(index * 10);
            }

            // Act
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks[i] = _wrapper.InvokeAsync<int>(identifier, index);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(i * 10, results[i]);
                _mockJSRuntime.Verify(x => x.InvokeAsync<int>(identifier, new object[] { i }), Times.Once);
            }
        }

        [Fact]
        public async Task InvokeVoidAsync_ConcurrentCalls_AllCallsComplete()
        {
            // Arrange
            const string identifier = "concurrentVoidFunction";
            var tasks = new Task[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = _wrapper.InvokeVoidAsync(identifier, i);
            }

            // Assert - Should complete without exceptions
            await Task.WhenAll(tasks);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task InvokeAsync_WithEmptyIdentifier_CallsJSRuntime()
        {
            // Arrange
            const string identifier = "";
            const string expectedResult = "empty";

            _mockJSRuntime
                .Setup(x => x.InvokeAsync<string>(identifier, It.IsAny<object[]>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _wrapper.InvokeAsync<string>(identifier);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<string>(identifier, It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WithSpecialCharactersInIdentifier_CallsJSRuntime()
        {
            // Arrange
            const string identifier = "window['special-function']";
            const bool expectedResult = true;

            _mockJSRuntime
                .Setup(x => x.InvokeAsync<bool>(identifier, It.IsAny<object[]>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _wrapper.InvokeAsync<bool>(identifier);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockJSRuntime.Verify(x => x.InvokeAsync<bool>(identifier, It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_TaskCancellation_PropagatesCancellation()
        {
            // Arrange
            const string identifier = "longRunning";
            var tcs = new TaskCompletionSource<string>();
            
            _mockJSRuntime
                .Setup(x => x.InvokeAsync<string>(identifier, It.IsAny<object[]>()))
                .Returns(new ValueTask<string>(tcs.Task));

            // Act
            var task = _wrapper.InvokeAsync<string>(identifier);
            tcs.SetCanceled();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        #endregion

        #region Helper Classes

        private class TestComplexType
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        #endregion
    }
}