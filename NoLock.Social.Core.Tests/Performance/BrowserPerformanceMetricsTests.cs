using System;
using FluentAssertions;
using NoLock.Social.Core.Performance;
using Xunit;

namespace NoLock.Social.Core.Tests.Performance
{
    public class BrowserPerformanceMetricsTests
    {
        [Theory]
        [InlineData(1000.0, 500.0, 1000.0, 250.0, 50.0, 0.1, 10485760, 52428800)]
        [InlineData(2000.0, 800.0, 1500.0, 400.0, 100.0, 0.2, 20971520, 104857600)]
        [InlineData(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0, 0)]
        [InlineData(5000.0, 2500.0, 4500.0, 1200.0, 150.0, 0.5, 31457280, 157286400)]
        public void BrowserPerformanceMetrics_ShouldStoreValuesCorrectly(
            double navigationStart, double domContentLoaded, double loadComplete,
            double firstContentfulPaint, double firstInputDelay, double cumulativeLayoutShift,
            long usedJSHeapSize, long totalJSHeapSize)
        {
            // Arrange & Act
            var metrics = new BrowserPerformanceMetrics
            {
                NavigationStart = navigationStart,
                DomContentLoaded = domContentLoaded,
                LoadComplete = loadComplete,
                FirstContentfulPaint = firstContentfulPaint,
                FirstInputDelay = firstInputDelay,
                CumulativeLayoutShift = cumulativeLayoutShift,
                UsedJSHeapSize = usedJSHeapSize,
                TotalJSHeapSize = totalJSHeapSize
            };

            // Assert
            metrics.NavigationStart.Should().Be(navigationStart);
            metrics.DomContentLoaded.Should().Be(domContentLoaded);
            metrics.LoadComplete.Should().Be(loadComplete);
            metrics.FirstContentfulPaint.Should().Be(firstContentfulPaint);
            metrics.FirstInputDelay.Should().Be(firstInputDelay);
            metrics.CumulativeLayoutShift.Should().Be(cumulativeLayoutShift);
            metrics.UsedJSHeapSize.Should().Be(usedJSHeapSize);
            metrics.TotalJSHeapSize.Should().Be(totalJSHeapSize);
        }

        [Fact]
        public void BrowserPerformanceMetrics_DefaultValues_ShouldBeZero()
        {
            // Arrange & Act
            var metrics = new BrowserPerformanceMetrics();

            // Assert
            metrics.NavigationStart.Should().Be(0);
            metrics.DomContentLoaded.Should().Be(0);
            metrics.LoadComplete.Should().Be(0);
            metrics.FirstContentfulPaint.Should().Be(0);
            metrics.FirstInputDelay.Should().Be(0);
            metrics.CumulativeLayoutShift.Should().Be(0);
            metrics.UsedJSHeapSize.Should().Be(0);
            metrics.TotalJSHeapSize.Should().Be(0);
        }

        [Theory]
        [InlineData(10485760, 52428800, 20.0)] // 10MB used, 50MB total = 20% usage
        [InlineData(26214400, 52428800, 50.0)] // 25MB used, 50MB total = 50% usage
        [InlineData(52428800, 52428800, 100.0)] // 50MB used, 50MB total = 100% usage
        [InlineData(0, 52428800, 0.0)] // 0MB used, 50MB total = 0% usage
        public void BrowserPerformanceMetrics_MemoryUsagePercentage_ShouldCalculateCorrectly(
            long usedHeap, long totalHeap, double expectedPercentage)
        {
            // Arrange
            var metrics = new BrowserPerformanceMetrics
            {
                UsedJSHeapSize = usedHeap,
                TotalJSHeapSize = totalHeap
            };

            // Act
            var usagePercentage = totalHeap > 0 
                ? (double)metrics.UsedJSHeapSize / metrics.TotalJSHeapSize * 100 
                : 0;

            // Assert
            usagePercentage.Should().BeApproximately(expectedPercentage, 0.01);
        }
    }
}