using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Interfaces
{
    /// <summary>
    /// Service for monitoring network connectivity status and changes
    /// Provides real-time connectivity detection using browser APIs
    /// </summary>
    public interface IConnectivityService : IDisposable
    {
        /// <summary>
        /// Get the current network connectivity status
        /// </summary>
        /// <returns>True if online, false if offline</returns>
        Task<bool> IsOnlineAsync();

        /// <summary>
        /// Start monitoring connectivity changes
        /// Begins listening to browser online/offline events
        /// </summary>
        /// <returns>Task that completes when monitoring starts</returns>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stop monitoring connectivity changes
        /// Stops listening to browser online/offline events
        /// </summary>
        /// <returns>Task that completes when monitoring stops</returns>
        Task StopMonitoringAsync();

        /// <summary>
        /// Event raised when the device goes online
        /// </summary>
        event EventHandler<ConnectivityEventArgs>? OnOnline;

        /// <summary>
        /// Event raised when the device goes offline
        /// </summary>
        event EventHandler<ConnectivityEventArgs>? OnOffline;
    }

    /// <summary>
    /// Event arguments for connectivity change events
    /// </summary>
    public class ConnectivityEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the device is currently online
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// Timestamp when the connectivity change occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Previous connectivity state before this change
        /// </summary>
        public bool? PreviousState { get; set; }
    }
}