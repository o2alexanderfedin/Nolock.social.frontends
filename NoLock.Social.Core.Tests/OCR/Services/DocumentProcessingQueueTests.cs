using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Comprehensive unit tests for DocumentProcessingQueue service with high coverage.
    /// Uses data-driven testing with xUnit Theory and mocked dependencies.
    /// </summary>
    public class DocumentProcessingQueueTests : IDisposable
    {
        private readonly DocumentProcessingQueue _queue;
        private readonly OCRSubmissionRequest _sampleRequest;

        public DocumentProcessingQueueTests()
        {
            _queue = new DocumentProcessingQueue();
            _sampleRequest = new OCRSubmissionRequest
            {
                DocumentType = DocumentType.Receipt,
                ImageData = System.Text.Encoding.UTF8.GetBytes("base64imagedata")
            };
            
            // Start the queue to allow document enqueuing
            _queue.StartAsync().GetAwaiter().GetResult();
        }

        #region EnqueueDocument Tests

        [Fact]
        public async Task EnqueueDocumentAsync_ValidRequest_ReturnsQueueId()
        {
            // Act
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);

            // Assert
            queueId.Should().NotBeNullOrEmpty();
            _queue.QueueCount.Should().Be(1);
        }

        [Fact]
        public async Task EnqueueDocumentAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _queue.EnqueueDocumentAsync(null));
        }

        [Theory]
        [InlineData(QueuePriority.Low, "Low priority")]
        [InlineData(QueuePriority.Normal, "Normal priority")]
        [InlineData(QueuePriority.High, "High priority")]
        [InlineData(QueuePriority.Critical, "Critical priority")]
        public async Task EnqueueDocumentAsync_WithDifferentPriorities_EnqueuesSuccessfully(
            QueuePriority priority, string scenario)
        {
            // Act
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest, priority);

            // Assert
            queueId.Should().NotBeNullOrEmpty($"Failed for {scenario}");
            var documents = await _queue.GetQueuedDocumentsAsync();
            documents.Should().HaveCount(1);
            documents.First().Priority.Should().Be(priority);
        }

        [Fact]
        public async Task EnqueueDocumentAsync_WithMetadata_StoresMetadata()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                ["UserId"] = "user123",
                ["SessionId"] = "session456",
                ["CustomField"] = 42
            };

            // Act
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal, metadata);

            // Assert
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.Should().NotBeNull();
            document.Metadata.Should().ContainKey("UserId");
            document.Metadata["UserId"].Should().Be("user123");
        }

        [Fact]
        public async Task EnqueueDocumentAsync_WhenQueueStopping_ThrowsInvalidOperationException()
        {
            // Arrange
            // Queue is already started in constructor
            await _queue.StopAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _queue.EnqueueDocumentAsync(_sampleRequest));
        }

        [Fact]
        public async Task EnqueueDocumentAsync_RaisesDocumentQueuedEvent()
        {
            // Arrange
            QueuedDocumentEventArgs eventArgs = null;
            _queue.DocumentQueued += (sender, args) => eventArgs = args;

            // Act
            await _queue.EnqueueDocumentAsync(_sampleRequest);

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs.Document.Should().NotBeNull();
            eventArgs.Document.Status.Should().Be(QueuedDocumentStatus.Queued);
        }

        [Fact]
        public async Task EnqueueDocumentAsync_MultipleDocuments_UpdatesQueuePositions()
        {
            // Act
            var queueId1 = await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal);
            var queueId2 = await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.High);
            var queueId3 = await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Low);

            // Assert
            var documents = await _queue.GetQueuedDocumentsAsync();
            documents.Should().HaveCount(3);
            documents.Should().BeInDescendingOrder(d => d.Priority);
        }

        #endregion

        #region GetQueuedDocuments Tests

        [Fact]
        public async Task GetQueuedDocumentsAsync_EmptyQueue_ReturnsEmptyList()
        {
            // Act
            var documents = await _queue.GetQueuedDocumentsAsync();

            // Assert
            documents.Should().BeEmpty();
        }

        [Theory]
        [InlineData(1, "single document")]
        [InlineData(5, "multiple documents")]
        [InlineData(10, "many documents")]
        public async Task GetQueuedDocumentsAsync_WithDocuments_ReturnsOrderedList(
            int documentCount, string scenario)
        {
            // Arrange
            var queueIds = new List<string>();
            for (int i = 0; i < documentCount; i++)
            {
                var priority = (QueuePriority)(i % 4);
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest, priority);
                queueIds.Add(queueId);
            }

            // Act
            var documents = await _queue.GetQueuedDocumentsAsync();

            // Assert
            documents.Should().HaveCount(documentCount, $"Failed for {scenario}");
            documents.Should().BeInDescendingOrder(d => d.Priority)
                .And.ThenBeInAscendingOrder(d => d.QueuedAt);
        }

        #endregion

        #region GetQueuedDocument Tests

        [Fact]
        public async Task GetQueuedDocumentAsync_ExistingDocument_ReturnsDocument()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);

            // Act
            var document = await _queue.GetQueuedDocumentAsync(queueId);

            // Assert
            document.Should().NotBeNull();
            document.QueueId.Should().Be(queueId);
        }

        [Fact]
        public async Task GetQueuedDocumentAsync_NonExistentDocument_ReturnsNull()
        {
            // Act
            var document = await _queue.GetQueuedDocumentAsync("nonexistent");

            // Assert
            document.Should().BeNull();
        }

        [Theory]
        [InlineData(null, "null queueId")]
        [InlineData("", "empty queueId")]
        [InlineData("   ", "whitespace queueId")]
        public async Task GetQueuedDocumentAsync_InvalidQueueId_ThrowsArgumentNullException(
            string queueId, string scenario)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _queue.GetQueuedDocumentAsync(queueId));
        }

        #endregion

        #region RemoveDocument Tests

        [Fact]
        public async Task RemoveDocumentAsync_ExistingDocument_RemovesSuccessfully()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);

            // Act
            var result = await _queue.RemoveDocumentAsync(queueId);

            // Assert
            result.Should().BeTrue();
            _queue.QueueCount.Should().Be(0);
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.Should().BeNull();
        }

        [Fact]
        public async Task RemoveDocumentAsync_NonExistentDocument_ReturnsFalse()
        {
            // Act
            var result = await _queue.RemoveDocumentAsync("nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task RemoveDocumentAsync_RaisesProcessingCompletedEvent()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            QueuedDocumentEventArgs eventArgs = null;
            _queue.ProcessingCompleted += (sender, args) => eventArgs = args;

            // Act
            await _queue.RemoveDocumentAsync(queueId);

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs.Document.QueueId.Should().Be(queueId);
        }

        #endregion

        #region State Management Tests

        [Fact]
        public async Task StartAsync_ChangesStateToRunning()
        {
            // Arrange - Create a new queue that hasn't been started
            var freshQueue = new DocumentProcessingQueue();
            
            // Act
            await freshQueue.StartAsync();

            // Assert
            freshQueue.CurrentState.Should().Be(QueueState.Running);
        }

        [Fact]
        public async Task PauseProcessingAsync_ChangesStateToPaused()
        {
            // Arrange - Queue is already started in constructor

            // Act
            await _queue.PauseProcessingAsync();

            // Assert
            _queue.CurrentState.Should().Be(QueueState.Paused);
        }

        [Fact]
        public async Task ResumeProcessingAsync_ChangesStateToRunning()
        {
            // Arrange - Queue is already started in constructor
            await _queue.PauseProcessingAsync();

            // Act
            await _queue.ResumeProcessingAsync();

            // Assert
            _queue.CurrentState.Should().Be(QueueState.Running);
        }

        [Fact]
        public async Task StopAsync_ChangesStateToStopped()
        {
            // Arrange - Queue is already started in constructor

            // Act
            await _queue.StopAsync();

            // Assert
            _queue.CurrentState.Should().Be(QueueState.Stopped);
        }

        [Fact]
        public async Task StateChanges_RaiseQueueStateChangedEvent()
        {
            // Arrange - Create fresh queue to track all state changes from beginning
            var freshQueue = new DocumentProcessingQueue();
            var stateChanges = new List<(QueueState Previous, QueueState New)>();
            freshQueue.QueueStateChanged += (sender, args) => 
                stateChanges.Add((args.PreviousState, args.CurrentState));

            // Act
            await freshQueue.StartAsync();
            await freshQueue.PauseProcessingAsync();
            await freshQueue.ResumeProcessingAsync();
            await freshQueue.StopAsync();

            // Assert
            stateChanges.Should().Contain(x => x.Previous == QueueState.Stopped && x.New == QueueState.Running);
            stateChanges.Should().Contain(x => x.Previous == QueueState.Running && x.New == QueueState.Paused);
            stateChanges.Should().Contain(x => x.Previous == QueueState.Paused && x.New == QueueState.Running);
            stateChanges.Should().Contain(x => x.Previous == QueueState.Running && x.New == QueueState.Stopping);
            stateChanges.Should().Contain(x => x.Previous == QueueState.Stopping && x.New == QueueState.Stopped);
        }

        #endregion

        #region Cancel and Retry Tests

        [Fact]
        public async Task CancelDocumentProcessingAsync_CancellableDocument_CancelsSuccessfully()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var document = await _queue.GetQueuedDocumentAsync(queueId);

            // Act
            var result = await _queue.CancelDocumentProcessingAsync(queueId);

            // Assert
            result.Should().BeTrue();
            document.Status.Should().Be(QueuedDocumentStatus.Cancelled);
        }

        [Theory]
        [InlineData(null, "null queueId")]
        [InlineData("", "empty queueId")]
        public async Task CancelDocumentProcessingAsync_InvalidQueueId_ThrowsArgumentNullException(
            string queueId, string scenario)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _queue.CancelDocumentProcessingAsync(queueId));
        }

        [Fact]
        public async Task CancelDocumentProcessingAsync_NonExistentDocument_ReturnsFalse()
        {
            // Act
            var result = await _queue.CancelDocumentProcessingAsync("nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task CancelDocumentProcessingAsync_RaisesEvents()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var statusChangedEvents = new List<QueuedDocumentEventArgs>();
            var completedEvents = new List<QueuedDocumentEventArgs>();
            
            _queue.ProcessingStatusChanged += (sender, args) => statusChangedEvents.Add(args);
            _queue.ProcessingCompleted += (sender, args) => completedEvents.Add(args);

            // Act
            await _queue.CancelDocumentProcessingAsync(queueId);

            // Assert
            statusChangedEvents.Should().HaveCount(1);
            completedEvents.Should().HaveCount(1);
        }

        [Fact]
        public async Task RetryDocumentAsync_FailedDocument_PreparesForRetry()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            
            // Simulate failed processing using reflection
            var method = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Failed, null, "Processing failed", null, CancellationToken.None 
            });

            // Act
            var result = await _queue.RetryDocumentAsync(queueId);

            // Assert
            result.Should().BeTrue();
            document.Status.Should().Be(QueuedDocumentStatus.Queued);
            document.RetryAttempts.Should().Be(1);
        }

        [Fact]
        public async Task RetryDocumentAsync_NonRetryableDocument_ThrowsInvalidOperationException()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            // Document in queued state (default) is not retryable - it needs to be in failed state

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _queue.RetryDocumentAsync(queueId));
        }

        #endregion

        #region ClearCompletedDocuments Tests

        [Theory]
        [InlineData(0, 5, 0, "no completed documents")]
        [InlineData(3, 2, 3, "some completed documents")]
        [InlineData(5, 0, 5, "all completed documents")]
        public async Task ClearCompletedDocumentsAsync_RemovesOnlyCompletedDocuments(
            int completedCount, int pendingCount, int expectedRemoved, string scenario)
        {
            // Arrange
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            for (int i = 0; i < completedCount; i++)
            {
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
                await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                    queueId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
                });
            }
            
            for (int i = 0; i < pendingCount; i++)
            {
                await _queue.EnqueueDocumentAsync(_sampleRequest);
            }

            // Act
            var removedCount = await _queue.ClearCompletedDocumentsAsync();

            // Assert
            removedCount.Should().Be(expectedRemoved, $"Failed for {scenario}");
            _queue.QueueCount.Should().Be(pendingCount);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public async Task GetStatisticsAsync_ReturnsValidStatistics()
        {
            // Arrange - Queue is already started in constructor
            var queueId1 = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var queueId2 = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            // Use reflection to update status
            var method = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId1, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });
            await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId2, QueuedDocumentStatus.Failed, null, "Processing failed", null, CancellationToken.None 
            });

            // Act
            var stats = await _queue.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalProcessed.Should().Be(2);
            stats.SuccessfullyProcessed.Should().Be(1);
            stats.FailedProcessing.Should().Be(1);
            stats.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Theory]
        [InlineData(100, 200, 150, "average of two processing times")]
        [InlineData(50, 150, 100, "different processing times")]
        public async Task GetStatisticsAsync_CalculatesAverageProcessingTime(
            int time1Ms, int time2Ms, double expectedAverage, string scenario)
        {
            // Arrange
            var queueId1 = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var queueId2 = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            var doc1 = await _queue.GetQueuedDocumentAsync(queueId1);
            var doc2 = await _queue.GetQueuedDocumentAsync(queueId2);
            
            // Simulate processing times
            doc1.ProcessingTimeMs = time1Ms;
            doc2.ProcessingTimeMs = time2Ms;
            
            // Use reflection to update status
            var method = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId1, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });
            await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId2, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });

            // Act
            var stats = await _queue.GetStatisticsAsync();

            // Assert
            stats.AverageProcessingTimeMs.Should().BeApproximately(expectedAverage, 0.1, scenario);
        }

        #endregion

        #region UpdateDocumentStatus Tests

        [Theory]
        [InlineData(QueuedDocumentStatus.Processing, "processing status")]
        [InlineData(QueuedDocumentStatus.Completed, "completed status")]
        [InlineData(QueuedDocumentStatus.Failed, "failed status")]
        [InlineData(QueuedDocumentStatus.Cancelled, "cancelled status")]
        public async Task UpdateDocumentStatusAsync_ValidStatus_UpdatesSuccessfully(
            QueuedDocumentStatus status, string scenario)
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);

            // Act
            var method = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var result = await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId, status, null, null, null, CancellationToken.None 
            });

            // Assert
            result.Should().BeTrue($"Failed for {scenario}");
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.Status.Should().Be(status);
        }

        [Fact]
        public async Task UpdateDocumentStatusAsync_WithOCRStatus_UpdatesOCRFields()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var ocrStatus = new OCRStatusResponse
            {
                TrackingId = Guid.NewGuid().ToString(),
                Status = OCRProcessingStatus.Complete,
                ProgressPercentage = 100,
                ResultData = new OCRResultData
                {
                    ConfidenceScore = 95.0,
                    Metrics = new ProcessingMetrics { ProcessingTimeMs = 150 }
                }
            };

            // Use reflection to call internal method
            var method = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var result = await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Completed, ocrStatus, null, null, CancellationToken.None 
            });

            // Assert
            result.Should().BeTrue();
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.ProcessingResult.Should().NotBeNull();
            document.ProcessingResult.ResultData?.ConfidenceScore.Should().Be(95.0);
        }

        [Fact]
        public async Task UpdateDocumentStatusAsync_WithError_StoresErrorDetails()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);

            // Use reflection to call internal method
            var method = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Failed, null, "OCR processing failed", "OCR_TIMEOUT", CancellationToken.None 
            });

            // Assert
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.ErrorMessage.Should().Be("OCR processing failed");
            document.ErrorCode.Should().Be("OCR_TIMEOUT");
        }

        [Fact]
        public async Task UpdateDocumentStatusAsync_RaisesProcessingStatusChangedEvent()
        {
            // Arrange
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            QueuedDocumentEventArgs eventArgs = null;
            _queue.ProcessingStatusChanged += (sender, args) => eventArgs = args;

            // Use reflection to call internal method
            var method = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            await (Task<bool>)method.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Processing, null, null, null, CancellationToken.None 
            });

            // Assert
            eventArgs.Should().NotBeNull();
            eventArgs.Document.Status.Should().Be(QueuedDocumentStatus.Processing);
        }

        #endregion

        #region GetNextDocumentForProcessing Tests

        [Fact]
        public void GetNextDocumentForProcessing_WithQueuedDocuments_ReturnsHighestPriority()
        {
            // Arrange
            _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Low).Wait();
            _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Critical).Wait();
            _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal).Wait();

            // Use reflection to call internal method
            var method = typeof(DocumentProcessingQueue).GetMethod("GetNextDocumentForProcessing",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var document = (QueuedDocument)method.Invoke(_queue, null);

            // Assert
            document.Should().NotBeNull();
            document.Priority.Should().Be(QueuePriority.Critical);
        }

        [Fact]
        public void GetNextDocumentForProcessing_EmptyQueue_ReturnsNull()
        {
            // Use reflection to call internal method
            var method = typeof(DocumentProcessingQueue).GetMethod("GetNextDocumentForProcessing",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var document = (QueuedDocument)method.Invoke(_queue, null);

            // Assert
            document.Should().BeNull();
        }

        [Fact]
        public void GetNextDocumentForProcessing_AllProcessing_ReturnsNull()
        {
            // Arrange
            var queueId = _queue.EnqueueDocumentAsync(_sampleRequest).Result;
            
            // Use reflection to update status
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Processing, null, null, null, CancellationToken.None 
            });

            // Use reflection to get next document
            var getMethod = typeof(DocumentProcessingQueue).GetMethod("GetNextDocumentForProcessing",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var document = (QueuedDocument)getMethod.Invoke(_queue, null);

            // Assert
            document.Should().BeNull();
        }

        #endregion

        #region Property Tests

        [Theory]
        [InlineData(0, 0, 0, "empty queue")]
        [InlineData(3, 0, 0, "only queued documents")]
        [InlineData(2, 1, 0, "mixed queued and processing")]
        [InlineData(0, 0, 5, "only completed documents")]
        [InlineData(2, 1, 3, "all status types")]
        public async Task QueueCounts_ReflectCorrectNumbers(
            int queuedCount, int processingCount, int completedCount, string scenario)
        {
            // Arrange
            for (int i = 0; i < queuedCount; i++)
            {
                await _queue.EnqueueDocumentAsync(_sampleRequest);
            }
            
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            
            for (int i = 0; i < processingCount; i++)
            {
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
                await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                    queueId, QueuedDocumentStatus.Processing, null, null, null, CancellationToken.None 
                });
            }
            
            for (int i = 0; i < completedCount; i++)
            {
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
                await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                    queueId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
                });
            }

            // Assert
            _queue.QueueCount.Should().Be(queuedCount, $"Failed for {scenario}");
            _queue.ProcessingCount.Should().Be(processingCount, $"Failed for {scenario}");
            _queue.CompletedCount.Should().Be(completedCount, $"Failed for {scenario}");
        }

        #endregion

        #region Concurrent Operations Tests

        [Fact]
        public async Task EnqueueDocumentAsync_ConcurrentEnqueues_HandledSafely()
        {
            // Arrange
            var tasks = new List<Task<string>>();
            var documentCount = 50;

            // Act
            for (int i = 0; i < documentCount; i++)
            {
                tasks.Add(_queue.EnqueueDocumentAsync(_sampleRequest));
            }
            
            var queueIds = await Task.WhenAll(tasks);

            // Assert
            queueIds.Should().HaveCount(documentCount);
            queueIds.Should().OnlyContain(id => !string.IsNullOrEmpty(id));
            queueIds.Distinct().Should().HaveCount(documentCount);
            _queue.QueueCount.Should().Be(documentCount);
        }

        [Fact]
        public async Task MixedConcurrentOperations_HandledSafely()
        {
            // Arrange
            var queueIds = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                queueIds.Add(await _queue.EnqueueDocumentAsync(_sampleRequest));
            }

            var tasks = new List<Task>();

            // Act - Mix of operations
            tasks.Add(_queue.EnqueueDocumentAsync(_sampleRequest));
            tasks.Add(_queue.RemoveDocumentAsync(queueIds[0]));
            tasks.Add(_queue.GetQueuedDocumentsAsync());
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            tasks.Add((Task)updateMethod.Invoke(_queue, new object[] { 
                queueIds[1], QueuedDocumentStatus.Processing, null, null, null, CancellationToken.None 
            }));
            tasks.Add(_queue.CancelDocumentProcessingAsync(queueIds[2]));
            tasks.Add(_queue.ClearCompletedDocumentsAsync());

            await Task.WhenAll(tasks);

            // Assert - Queue should be in consistent state
            var documents = await _queue.GetQueuedDocumentsAsync();
            documents.Should().NotBeNull();
            _queue.QueueCount.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_ReleasesResources()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            queue.StartAsync().Wait(); // Start the queue first
            queue.EnqueueDocumentAsync(_sampleRequest).Wait();

            // Act
            queue.Dispose();

            // Assert
            var ex = Assert.Throws<AggregateException>(() => queue.EnqueueDocumentAsync(_sampleRequest).Wait());
            ex.InnerException.Should().BeOfType<ObjectDisposedException>();
        }

        [Fact]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();

            // Act & Assert - Should not throw
            queue.Dispose();
            queue.Dispose();
        }

        #endregion

        public void Dispose()
        {
            _queue?.Dispose();
        }
    }
}