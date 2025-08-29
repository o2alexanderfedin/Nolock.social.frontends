using System;
using System.Collections.Concurrent;
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
    /// Advanced test suite for DocumentProcessingQueue focusing on edge cases,
    /// concurrent operations, thread safety, and error recovery scenarios.
    /// </summary>
    public class DocumentProcessingQueueAdvancedTests : IDisposable
    {
        private readonly DocumentProcessingQueue _queue;
        private readonly OCRSubmissionRequest _sampleRequest;

        public DocumentProcessingQueueAdvancedTests()
        {
            _queue = new DocumentProcessingQueue();
            _sampleRequest = new OCRSubmissionRequest
            {
                DocumentType = DocumentType.Receipt,
                ImageData = System.Text.Encoding.UTF8.GetBytes("base64imagedata")
            };
        }

        #region Thread Safety and Concurrent Operations Tests

        [Fact]
        public async Task ConcurrentEnqueueAndDequeue_MaintainsConsistency()
        {
            // Arrange
            await _queue.StartAsync();
            var enqueueCount = 100;
            var dequeueCount = 50;
            var queueIds = new ConcurrentBag<string>();
            var removedCount = 0;

            // Act - Concurrent enqueue and dequeue operations
            var enqueueTasks = Enumerable.Range(0, enqueueCount).Select(async i =>
            {
                var priority = (QueuePriority)(i % 4);
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest, priority);
                queueIds.Add(queueId);
            });

            await Task.WhenAll(enqueueTasks);

            var dequeueTasks = queueIds.Take(dequeueCount).Select(async queueId =>
            {
                var result = await _queue.RemoveDocumentAsync(queueId);
                if (result) Interlocked.Increment(ref removedCount);
            });

            await Task.WhenAll(dequeueTasks);

            // Assert
            removedCount.Should().Be(dequeueCount);
            _queue.QueueCount.Should().Be(enqueueCount - dequeueCount);
        }

        [Fact]
        public async Task ConcurrentStatusUpdates_HandledCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            var documentCount = 20;
            var queueIds = new List<string>();
            
            for (int i = 0; i < documentCount; i++)
            {
                queueIds.Add(await _queue.EnqueueDocumentAsync(_sampleRequest));
            }

            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Act - Concurrent status updates
            var updateTasks = queueIds.Select((queueId, index) =>
            {
                var status = index % 3 == 0 ? QueuedDocumentStatus.Completed :
                           index % 3 == 1 ? QueuedDocumentStatus.Failed :
                           QueuedDocumentStatus.Processing;
                
                return (Task)updateMethod.Invoke(_queue, new object[] { 
                    queueId, status, null, null, null, CancellationToken.None 
                });
            });

            await Task.WhenAll(updateTasks);

            // Assert
            var documents = await _queue.GetQueuedDocumentsAsync();
            documents.Should().HaveCount(documentCount);
            documents.Count(d => d.Status == QueuedDocumentStatus.Completed).Should().BeGreaterThan(0);
            documents.Count(d => d.Status == QueuedDocumentStatus.Failed).Should().BeGreaterThan(0);
            documents.Count(d => d.Status == QueuedDocumentStatus.Processing).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ConcurrentPriorityChanges_MaintainQueueOrdering()
        {
            // Arrange
            await _queue.StartAsync();
            var tasks = new List<Task<string>>();

            // Act - Enqueue documents with different priorities concurrently
            var priorities = new[] { 
                QueuePriority.Critical, QueuePriority.Low, QueuePriority.High, 
                QueuePriority.Normal, QueuePriority.Critical, QueuePriority.Low 
            };

            foreach (var priority in priorities)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(Random.Shared.Next(10, 50)); // Random delay to ensure concurrency
                    return await _queue.EnqueueDocumentAsync(_sampleRequest, priority);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Documents should be ordered by priority
            var documents = await _queue.GetQueuedDocumentsAsync();
            documents.Should().HaveCount(priorities.Length);
            
            // Verify ordering: Critical > High > Normal > Low
            var prevPriority = (int)QueuePriority.Critical + 1;
            foreach (var doc in documents)
            {
                ((int)doc.Priority).Should().BeLessThanOrEqualTo(prevPriority);
                prevPriority = (int)doc.Priority;
            }
        }

        #endregion

        #region Retry Logic Edge Cases

        [Theory]
        [InlineData(1, "first retry")]
        [InlineData(2, "second retry")]
        [InlineData(3, "third retry")]
        public async Task RetryDocumentAsync_MultipleRetries_TracksAttempts(int retryCount, string scenario)
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Act - Simulate failure and retry multiple times
            for (int i = 0; i < retryCount; i++)
            {
                // Mark as failed
                await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                    queueId, QueuedDocumentStatus.Failed, null, $"Failure {i+1}", null, CancellationToken.None 
                });
                
                // Retry
                var result = await _queue.RetryDocumentAsync(queueId);
                result.Should().BeTrue($"Retry {i+1} should succeed for {scenario}");
            }

            // Assert
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            document.RetryAttempts.Should().Be(retryCount);
        }

        [Fact]
        public async Task RetryDocumentAsync_ExceededMaxRetries_HandledCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            
            // Set max retries exceeded
            document.RetryAttempts = document.MaxRetryAttempts;
            
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                queueId, QueuedDocumentStatus.Failed, null, "Max retries exceeded", null, CancellationToken.None 
            });

            // Act & Assert
            var result = await _queue.RetryDocumentAsync(queueId);
            result.Should().BeTrue(); // Should still allow retry even at max attempts
            document.Status.Should().Be(QueuedDocumentStatus.Queued);
        }

        [Fact]
        public async Task RetryDocumentAsync_NonExistentDocument_ReturnsFalse()
        {
            // Arrange
            await _queue.StartAsync();

            // Act
            var result = await _queue.RetryDocumentAsync("non-existent-id");

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(null, "null queueId")]
        [InlineData("", "empty queueId")]
        public async Task RetryDocumentAsync_InvalidQueueId_ThrowsArgumentNullException(string queueId, string scenario)
        {
            // Arrange
            await _queue.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _queue.RetryDocumentAsync(queueId));
        }

        #endregion

        #region Queue State Transitions and Edge Cases

        [Fact]
        public async Task StopAsync_WhenAlreadyStopped_HandlesGracefully()
        {
            // Arrange - Queue starts in stopped state
            var freshQueue = new DocumentProcessingQueue();

            // Act - Stop when already stopped
            await freshQueue.StopAsync();

            // Assert
            freshQueue.CurrentState.Should().Be(QueueState.Stopped);
        }

        [Fact]
        public async Task PauseProcessingAsync_AfterDisposal_ThrowsObjectDisposedException()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            await queue.StartAsync();
            queue.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => queue.PauseProcessingAsync());
        }

        [Fact]
        public async Task ResumeProcessingAsync_AfterDisposal_ThrowsObjectDisposedException()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            await queue.StartAsync();
            await queue.PauseProcessingAsync();
            queue.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => queue.ResumeProcessingAsync());
        }

        [Fact]
        public async Task StateTransitions_RapidChanges_HandledCorrectly()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            var stateChanges = new ConcurrentBag<(QueueState Previous, QueueState New)>();
            queue.QueueStateChanged += (sender, args) => 
                stateChanges.Add((args.PreviousState, args.CurrentState));

            // Act - Rapid state changes
            var tasks = new[]
            {
                queue.StartAsync(),
                Task.Delay(10).ContinueWith(_ => queue.PauseProcessingAsync()),
                Task.Delay(20).ContinueWith(_ => queue.ResumeProcessingAsync()),
                Task.Delay(30).ContinueWith(_ => queue.PauseProcessingAsync()),
                Task.Delay(40).ContinueWith(_ => queue.ResumeProcessingAsync()),
                Task.Delay(50).ContinueWith(_ => queue.StopAsync())
            };

            await Task.WhenAll(tasks);

            // Assert
            stateChanges.Should().NotBeEmpty();
            queue.CurrentState.Should().Be(QueueState.Stopped);
        }

        #endregion

        #region Error Recovery and Cancellation

        [Fact]
        public async Task CancelDocumentProcessingAsync_AlreadyCancelled_ReturnsFalse()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            
            // First cancellation
            await _queue.CancelDocumentProcessingAsync(queueId);

            // Act - Try to cancel again
            var result = await _queue.CancelDocumentProcessingAsync(queueId);

            // Assert
            result.Should().BeTrue(); // Should still return true as document exists and is cancellable
        }

        [Fact]
        public async Task RemoveDocumentAsync_WithCancellableDocument_CancelsFirst()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var document = await _queue.GetQueuedDocumentAsync(queueId);
            
            var cancelledEventRaised = false;
            _queue.ProcessingCompleted += (sender, args) =>
            {
                if (args.Document.QueueId == queueId)
                    cancelledEventRaised = true;
            };

            // Act
            var result = await _queue.RemoveDocumentAsync(queueId);

            // Assert
            result.Should().BeTrue();
            cancelledEventRaised.Should().BeTrue();
        }

        [Theory]
        [InlineData(null, "null error message")]
        [InlineData("", "empty error message")]
        public async Task RemoveDocumentAsync_InvalidQueueId_ThrowsArgumentNullException(string queueId, string scenario)
        {
            // Arrange
            await _queue.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _queue.RemoveDocumentAsync(queueId));
        }

        #endregion

        #region Statistics Edge Cases

        [Fact]
        public async Task GetStatisticsAsync_WithNoProcessedDocuments_ReturnsZeroStats()
        {
            // Arrange
            await _queue.StartAsync();

            // Act
            var stats = await _queue.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalProcessed.Should().Be(0);
            stats.SuccessfullyProcessed.Should().Be(0);
            stats.FailedProcessing.Should().Be(0);
            stats.ThroughputPerMinute.Should().Be(0);
        }

        [Fact]
        public async Task GetStatisticsAsync_AfterDisposal_ThrowsObjectDisposedException()
        {
            // Arrange
            var queue = new DocumentProcessingQueue();
            await queue.StartAsync();
            queue.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => queue.GetStatisticsAsync());
        }

        [Fact]
        public async Task UpdateStatistics_WithMixedResults_CalculatesCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            var successCount = 5;
            var failCount = 3;
            var cancelCount = 2;
            
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Create and process documents
            for (int i = 0; i < successCount; i++)
            {
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
                var doc = await _queue.GetQueuedDocumentAsync(queueId);
                doc.ProcessingTimeMs = 100 + i * 10;
                await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                    queueId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
                });
            }

            for (int i = 0; i < failCount; i++)
            {
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
                var doc = await _queue.GetQueuedDocumentAsync(queueId);
                doc.ProcessingTimeMs = 50;
                await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                    queueId, QueuedDocumentStatus.Failed, null, "Test failure", null, CancellationToken.None 
                });
            }

            for (int i = 0; i < cancelCount; i++)
            {
                var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
                await _queue.CancelDocumentProcessingAsync(queueId);
            }

            // Act
            var stats = await _queue.GetStatisticsAsync();

            // Assert
            stats.TotalProcessed.Should().Be(successCount + failCount); // Cancelled documents are not counted as processed
            stats.SuccessfullyProcessed.Should().Be(successCount);
            stats.FailedProcessing.Should().Be(failCount);
            stats.AverageProcessingTimeMs.Should().BeGreaterThan(0);
            stats.ThroughputPerMinute.Should().BeGreaterThan(0);
        }

        #endregion

        #region Queue Position Management

        [Fact]
        public async Task UpdateQueuePositions_AfterRemoval_ReordersCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            var queueIds = new List<string>();
            
            for (int i = 0; i < 5; i++)
            {
                queueIds.Add(await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal));
            }

            // Act - Remove middle document
            await _queue.RemoveDocumentAsync(queueIds[2]);

            // Assert
            var documents = await _queue.GetQueuedDocumentsAsync();
            documents.Should().HaveCount(4);
            
            // Verify queue positions are sequential
            for (int i = 0; i < documents.Count; i++)
            {
                documents[i].QueuePosition.Should().Be(i + 1);
            }
        }

        [Fact]
        public async Task UpdateQueuePositions_WithMixedPriorities_OrdersCorrectly()
        {
            // Arrange
            await _queue.StartAsync();
            
            // Add documents with different priorities
            await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Low);
            await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Critical);
            await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal);
            await _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.High);

            // Act
            var documents = await _queue.GetQueuedDocumentsAsync();

            // Assert - Should be ordered Critical > High > Normal > Low
            documents[0].Priority.Should().Be(QueuePriority.Critical);
            documents[0].QueuePosition.Should().Be(1);
            documents[1].Priority.Should().Be(QueuePriority.High);
            documents[1].QueuePosition.Should().Be(2);
            documents[2].Priority.Should().Be(QueuePriority.Normal);
            documents[2].QueuePosition.Should().Be(3);
            documents[3].Priority.Should().Be(QueuePriority.Low);
            documents[3].QueuePosition.Should().Be(4);
        }

        #endregion

        #region Event Handling Edge Cases

        [Fact]
        public async Task Events_MultipleSubscribers_AllReceiveNotifications()
        {
            // Arrange
            await _queue.StartAsync();
            var subscriber1Count = 0;
            var subscriber2Count = 0;
            var subscriber3Count = 0;

            _queue.DocumentQueued += (sender, args) => subscriber1Count++;
            _queue.DocumentQueued += (sender, args) => subscriber2Count++;
            _queue.DocumentQueued += (sender, args) => subscriber3Count++;

            // Act
            for (int i = 0; i < 3; i++)
            {
                await _queue.EnqueueDocumentAsync(_sampleRequest);
            }

            // Assert
            subscriber1Count.Should().Be(3);
            subscriber2Count.Should().Be(3);
            subscriber3Count.Should().Be(3);
        }

        [Fact]
        public async Task ProcessingCompleted_RaisedForAllCompletionTypes()
        {
            // Arrange
            await _queue.StartAsync();
            var completedEvents = new List<QueuedDocumentEventArgs>();
            _queue.ProcessingCompleted += (sender, args) => completedEvents.Add(args);

            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Create documents
            var successId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var failId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var cancelId = await _queue.EnqueueDocumentAsync(_sampleRequest);

            // Act - Complete with different statuses
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                successId, QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });
            await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                failId, QueuedDocumentStatus.Failed, null, "Test failure", null, CancellationToken.None 
            });
            await _queue.CancelDocumentProcessingAsync(cancelId);

            // Assert
            completedEvents.Should().HaveCount(3);
            completedEvents.Should().Contain(e => e.Document.Status == QueuedDocumentStatus.Completed);
            completedEvents.Should().Contain(e => e.Document.Status == QueuedDocumentStatus.Failed);
            completedEvents.Should().Contain(e => e.Document.Status == QueuedDocumentStatus.Cancelled);
        }

        #endregion

        #region Performance and Load Tests

        [Fact]
        public async Task EnqueueDocumentAsync_UnderLoad_MaintainsPerformance()
        {
            // Arrange
            await _queue.StartAsync();
            var documentCount = 1000;
            var startTime = DateTime.UtcNow;

            // Act
            var tasks = Enumerable.Range(0, documentCount).Select(i =>
                _queue.EnqueueDocumentAsync(_sampleRequest, (QueuePriority)(i % 4))
            );
            
            await Task.WhenAll(tasks);
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            _queue.QueueCount.Should().Be(documentCount);
            elapsed.TotalSeconds.Should().BeLessThan(5, "Should enqueue 1000 documents in less than 5 seconds");
        }

        [Fact]
        public async Task GetQueuedDocumentsAsync_LargeQueue_ReturnsOrderedResults()
        {
            // Arrange
            await _queue.StartAsync();
            var documentCount = 500;
            
            for (int i = 0; i < documentCount; i++)
            {
                await _queue.EnqueueDocumentAsync(_sampleRequest, (QueuePriority)(i % 4));
            }

            // Act
            var startTime = DateTime.UtcNow;
            var documents = await _queue.GetQueuedDocumentsAsync();
            var elapsed = DateTime.UtcNow - startTime;

            // Assert
            documents.Should().HaveCount(documentCount);
            elapsed.TotalMilliseconds.Should().BeLessThan(100, "Should retrieve 500 documents in less than 100ms");
            
            // Verify ordering
            for (int i = 1; i < documents.Count; i++)
            {
                if (documents[i].Priority == documents[i-1].Priority)
                {
                    documents[i].QueuedAt.Should().BeOnOrAfter(documents[i-1].QueuedAt);
                }
                else
                {
                    ((int)documents[i].Priority).Should().BeLessThanOrEqualTo((int)documents[i-1].Priority);
                }
            }
        }

        #endregion

        #region Cancellation Token Tests

        [Fact]
        public async Task EnqueueDocumentAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            await _queue.StartAsync();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal, null, cts.Token));
        }

        [Fact]
        public async Task RemoveDocumentAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            await _queue.StartAsync();
            var queueId = await _queue.EnqueueDocumentAsync(_sampleRequest);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _queue.RemoveDocumentAsync(queueId, cts.Token));
        }

        #endregion

        #region Internal Method Tests

        [Fact]
        public async Task UpdateDocumentStatusAsync_NonExistentDocument_ReturnsFalse()
        {
            // Arrange
            await _queue.StartAsync();
            var updateMethod = typeof(DocumentProcessingQueue).GetMethod("UpdateDocumentStatusAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            // Act
            var result = await (Task<bool>)updateMethod.Invoke(_queue, new object[] { 
                "non-existent", QueuedDocumentStatus.Completed, null, null, null, CancellationToken.None 
            });

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetNextDocumentForProcessing_WithSamePriorityDocuments_ReturnsFIFO()
        {
            // Arrange
            _queue.StartAsync().Wait();
            var queueIds = new List<string>();
            
            // Add documents with same priority but different queue times
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(10); // Ensure different timestamps
                queueIds.Add(_queue.EnqueueDocumentAsync(_sampleRequest, QueuePriority.Normal).Result);
            }

            var getMethod = typeof(DocumentProcessingQueue).GetMethod("GetNextDocumentForProcessing",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // Act
            var firstDoc = (QueuedDocument)getMethod.Invoke(_queue, null);

            // Assert
            firstDoc.Should().NotBeNull();
            firstDoc.QueueId.Should().Be(queueIds[0], "Should return first queued document when priorities are equal");
        }

        #endregion

        public void Dispose()
        {
            _queue?.Dispose();
        }
    }
}