using System;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Den.Dev.FrameDrop.Desktop.ViewModels
{
    public class TrayViewModel : IDisposable
    {
        private readonly Timer relativeTimeTimer;
        private DateTimeOffset? lastSyncTime;

        public TrayIcon? TrayIcon { get; set; }
        public NativeMenuItem? StatusMenuItem { get; set; }
        public NativeMenuItem? LastSyncMenuItem { get; set; }
        public NativeMenuItem? SyncNowMenuItem { get; set; }
        public NativeMenuItem? PauseMenuItem { get; set; }
        public NativeMenuItem? AuthMenuItem { get; set; }

        public TrayViewModel()
        {
            this.relativeTimeTimer = new Timer(30_000);
            this.relativeTimeTimer.Elapsed += (s, e) => this.RefreshLastSyncDisplay();
            this.relativeTimeTimer.Start();
        }

        public void UpdateToolTip(string text)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.TrayIcon != null)
                {
                    this.TrayIcon.ToolTipText = text;
                }
            });
        }

        public void UpdateStatus(string statusText, DateTimeOffset? syncTime)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.StatusMenuItem != null && this.StatusMenuItem.Header != statusText)
                {
                    this.StatusMenuItem.Header = statusText;
                }

                if (syncTime.HasValue)
                {
                    this.lastSyncTime = syncTime.Value;
                    this.ApplyLastSyncDisplay();
                }
            });
        }

        public void UpdateAuthState(bool isAuthenticated, string? gamertag)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.AuthMenuItem != null)
                {
                    var newHeader = isAuthenticated
                        ? $"Log Out ({gamertag ?? "Unknown"})"
                        : "Log In";
                    if (this.AuthMenuItem.Header != newHeader)
                    {
                        this.AuthMenuItem.Header = newHeader;
                    }
                }
            });
        }

        public void UpdatePauseState(bool isPaused)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.PauseMenuItem != null)
                {
                    var newHeader = isPaused ? "Resume Syncing" : "Pause Syncing";
                    if (this.PauseMenuItem.Header != newHeader)
                    {
                        this.PauseMenuItem.Header = newHeader;
                    }
                }
            });
        }

        public void UpdateSyncingState(bool isSyncing)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.SyncNowMenuItem != null)
                {
                    var newEnabled = !isSyncing;
                    if (this.SyncNowMenuItem.IsEnabled != newEnabled)
                    {
                        this.SyncNowMenuItem.IsEnabled = newEnabled;
                    }
                }
            });
        }

        /// <summary>
        /// Called from the 30-second timer to refresh relative time text.
        /// Dispatches to UI thread since the timer runs on a thread pool thread.
        /// </summary>
        private void RefreshLastSyncDisplay()
        {
            Dispatcher.UIThread.Post(() => this.ApplyLastSyncDisplay());
        }

        /// <summary>
        /// Directly updates LastSyncMenuItem.Header. Must be called on the UI thread.
        /// </summary>
        private void ApplyLastSyncDisplay()
        {
            if (this.LastSyncMenuItem == null)
            {
                return;
            }

            if (this.lastSyncTime == null)
            {
                if (this.LastSyncMenuItem.Header != "Last sync: Never")
                {
                    this.LastSyncMenuItem.Header = "Last sync: Never";
                }
                return;
            }

            var elapsed = DateTimeOffset.Now - this.lastSyncTime.Value;
            string text;
            if (elapsed.TotalSeconds < 60)
            {
                text = "Last sync: Just now";
            }
            else if (elapsed.TotalMinutes < 2)
            {
                text = "Last sync: 1 minute ago";
            }
            else if (elapsed.TotalMinutes < 60)
            {
                text = $"Last sync: {(int)elapsed.TotalMinutes} minutes ago";
            }
            else if (elapsed.TotalHours < 2)
            {
                text = "Last sync: 1 hour ago";
            }
            else if (elapsed.TotalHours < 24)
            {
                text = $"Last sync: {(int)elapsed.TotalHours} hours ago";
            }
            else
            {
                text = $"Last sync: {this.lastSyncTime.Value:g}";
            }

            if (this.LastSyncMenuItem.Header != text)
            {
                this.LastSyncMenuItem.Header = text;
            }
        }

        public void Dispose()
        {
            this.relativeTimeTimer.Stop();
            this.relativeTimeTimer.Dispose();
        }
    }
}
