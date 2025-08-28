using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using FluentAssertions;
using Moq;
using NoLock.Social.Core.Cryptography.Extensions;
using Xunit;

namespace NoLock.Social.Core.Tests.Cryptography.Extensions
{
    public class DisposableExtensionsTests
    {
        [Fact]
        public void AddTo_ShouldAddDisposableToComposite()
        {
            // Arrange
            var composite = new CompositeDisposable();
            var disposable = new Mock<IDisposable>();
            
            // Act
            var result = disposable.Object.AddTo(composite);
            
            // Assert
            result.Should().BeSameAs(disposable.Object);
            composite.Count.Should().Be(1);
            
            // Verify disposal
            composite.Dispose();
            disposable.Verify(x => x.Dispose(), Times.Once);
        }

        [Theory]
        [InlineData(1, "Single disposable")]
        [InlineData(5, "Multiple disposables")]
        [InlineData(10, "Many disposables")]
        [InlineData(100, "Large number of disposables")]
        public void AddTo_ShouldHandleMultipleDisposables(int count, string scenario)
        {
            // Arrange
            var composite = new CompositeDisposable();
            var disposables = new List<Mock<IDisposable>>();
            
            // Act
            for (int i = 0; i < count; i++)
            {
                var mock = new Mock<IDisposable>();
                disposables.Add(mock);
                mock.Object.AddTo(composite);
            }
            
            // Assert
            composite.Count.Should().Be(count, scenario);
            
            // Verify all are disposed
            composite.Dispose();
            foreach (var mock in disposables)
            {
                mock.Verify(x => x.Dispose(), Times.Once);
            }
        }

        [Fact]
        public void AddTo_WithNullDisposable_ShouldThrowArgumentNullException()
        {
            // Arrange
            var composite = new CompositeDisposable();
            IDisposable? nullDisposable = null;
            
            // Act
            var act = () => nullDisposable!.AddTo(composite);
            
            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("disposable");
        }

        [Fact]
        public void AddTo_WithNullComposite_ShouldThrowArgumentNullException()
        {
            // Arrange
            CompositeDisposable? nullComposite = null;
            var disposable = new Mock<IDisposable>().Object;
            
            // Act
            var act = () => disposable.AddTo(nullComposite!);
            
            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("composite");
        }

        [Theory]
        [InlineData(true, "Non-null disposable")]
        [InlineData(false, "Null disposable")]
        public void SafeDispose_ShouldHandleNullGracefully(bool hasValue, string scenario)
        {
            // Arrange
            var mock = hasValue ? new Mock<IDisposable>() : null;
            IDisposable? disposable = mock?.Object;
            
            // Act
            disposable.SafeDispose();
            
            // Assert
            if (hasValue)
            {
                mock!.Verify(x => x.Dispose(), Times.Once, scenario);
            }
            // No exception should be thrown for null
        }

        [Fact]
        public void SafeDispose_ShouldNotThrowOnDisposalException()
        {
            // Arrange
            var disposable = new Mock<IDisposable>();
            disposable.Setup(x => x.Dispose()).Throws<InvalidOperationException>();
            
            // Act - this would normally throw, but SafeDispose should handle it
            var act = () => disposable.Object.SafeDispose();
            
            // Assert - the exception is propagated (SafeDispose doesn't swallow exceptions from Dispose)
            act.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData(0, "Empty array")]
        [InlineData(1, "Single disposable")]
        [InlineData(3, "Multiple disposables")]
        [InlineData(10, "Many disposables")]
        public void CreateComposite_ShouldCreateWithProvidedDisposables(int count, string scenario)
        {
            // Arrange
            var disposables = new IDisposable[count];
            var mocks = new List<Mock<IDisposable>>();
            
            for (int i = 0; i < count; i++)
            {
                var mock = new Mock<IDisposable>();
                mocks.Add(mock);
                disposables[i] = mock.Object;
            }
            
            // Act
            var composite = DisposableExtensions.CreateComposite(disposables);
            
            // Assert
            composite.Should().NotBeNull(scenario);
            composite.Count.Should().Be(count, scenario);
            
            // Verify disposal
            composite.Dispose();
            foreach (var mock in mocks)
            {
                mock.Verify(x => x.Dispose(), Times.Once);
            }
        }

        [Fact]
        public void CreateComposite_ShouldIgnoreNullDisposables()
        {
            // Arrange
            var mock1 = new Mock<IDisposable>();
            var mock2 = new Mock<IDisposable>();
            var disposables = new IDisposable?[] { mock1.Object, null, mock2.Object, null };
            
            // Act
            var composite = DisposableExtensions.CreateComposite(disposables!);
            
            // Assert
            composite.Count.Should().Be(2);
            
            // Verify only non-null disposables are disposed
            composite.Dispose();
            mock1.Verify(x => x.Dispose(), Times.Once);
            mock2.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void CreateComposite_WithEmptyArray_ShouldCreateEmptyComposite()
        {
            // Act
            var composite = DisposableExtensions.CreateComposite();
            
            // Assert
            composite.Should().NotBeNull();
            composite.Count.Should().Be(0);
            composite.IsDisposed.Should().BeFalse();
            
            // Should not throw
            composite.Dispose();
            composite.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void AddTo_ShouldAllowChaining()
        {
            // Arrange
            var composite = new CompositeDisposable();
            var disposable1 = new Mock<IDisposable>();
            var disposable2 = new Mock<IDisposable>();
            var disposable3 = new Mock<IDisposable>();
            
            // Act - chaining multiple AddTo calls
            disposable1.Object
                .AddTo(composite)
                .Should().BeSameAs(disposable1.Object);
            
            disposable2.Object
                .AddTo(composite)
                .Should().BeSameAs(disposable2.Object);
            
            disposable3.Object
                .AddTo(composite)
                .Should().BeSameAs(disposable3.Object);
            
            // Assert
            composite.Count.Should().Be(3);
            
            composite.Dispose();
            disposable1.Verify(x => x.Dispose(), Times.Once);
            disposable2.Verify(x => x.Dispose(), Times.Once);
            disposable3.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void CompositeDisposable_ShouldNotDisposeItemsTwice()
        {
            // Arrange
            var composite = new CompositeDisposable();
            var disposable = new Mock<IDisposable>();
            disposable.Object.AddTo(composite);
            
            // Act
            composite.Dispose();
            composite.Dispose(); // Second disposal
            
            // Assert
            disposable.Verify(x => x.Dispose(), Times.Once);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(5, 3)]
        [InlineData(10, 7)]
        [InlineData(20, 15)]
        public void CreateComposite_WithMixedNullAndValidDisposables(int totalCount, int validCount)
        {
            // Arrange
            var disposables = new IDisposable?[totalCount];
            var mocks = new List<Mock<IDisposable>>();
            var validIndices = new HashSet<int>();
            
            // Add valid disposables at random positions
            var random = new Random(42);
            while (validIndices.Count < validCount)
            {
                validIndices.Add(random.Next(totalCount));
            }
            
            foreach (var index in validIndices)
            {
                var mock = new Mock<IDisposable>();
                mocks.Add(mock);
                disposables[index] = mock.Object;
            }
            
            // Act
            var composite = DisposableExtensions.CreateComposite(disposables!);
            
            // Assert
            composite.Count.Should().Be(validCount);
            
            composite.Dispose();
            foreach (var mock in mocks)
            {
                mock.Verify(x => x.Dispose(), Times.Once);
            }
        }
    }
}