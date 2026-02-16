using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Den.Dev.FrameDrop.Download;
using Den.Dev.FrameDrop.Desktop.ViewModels;
using Den.Dev.FrameDrop.Models;

namespace Den.Dev.FrameDrop.Desktop.Services
{
    public class SyncService
    {
        private readonly AuthService authService;
        private readonly TrayViewModel trayViewModel;
        private readonly SemaphoreSlim syncLock = new(1, 1);

        private CancellationTokenSource? cts;
        private bool isPaused;

        public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(15);

        public string OutputDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "FrameDrop");

        public bool IsPaused => this.isPaused;

        public SyncService(AuthService authService, TrayViewModel trayViewModel)
        {
            this.authService = authService;
            this.trayViewModel = trayViewModel;
        }

        public void Start()
        {
            this.Stop();
            this.cts = new CancellationTokenSource();
            _ = this.SyncLoopAsync(this.cts.Token);
        }

        public void Stop()
        {
            this.cts?.Cancel();
            this.cts?.Dispose();
            this.cts = null;
        }

        public void Pause()
        {
            this.isPaused = true;
        }

        public void Resume()
        {
            this.isPaused = false;
        }

        public async Task SyncOnceAsync()
        {
            if (!this.syncLock.Wait(0))
            {
                return; // Already syncing
            }

            try
            {
                await this.RunSyncCycleAsync(CancellationToken.None);
            }
            finally
            {
                this.syncLock.Release();
            }
        }

        private async Task SyncLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (!this.isPaused && this.syncLock.Wait(0))
                {
                    try
                    {
                        await this.RunSyncCycleAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    finally
                    {
                        this.syncLock.Release();
                    }
                }

                try
                {
                    await Task.Delay(this.SyncInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunSyncCycleAsync(CancellationToken ct)
        {
            if (!this.authService.HasValidTokens())
            {
                this.trayViewModel.UpdateStatus("Not authenticated", null);
                this.trayViewModel.UpdateAuthState(false, null);
                return;
            }

            try
            {
                this.trayViewModel.UpdateSyncingState(true);
                this.trayViewModel.UpdateStatus("Syncing...", null);

                var mediaClient = this.authService.CreateMediaClient();
                var captures = await mediaClient.ListAllCapturesAsync(null, ct);
                var totalCaptures = captures.Captures.Count;

                // Diff against local folder
                var outputDir = this.OutputDirectory;
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                var pending = new List<Capture>();
                foreach (var capture in captures.Captures)
                {
                    var ext = capture.CaptureType == CaptureType.Screenshot ? ".png" : ".mp4";
                    var filePath = Path.Combine(outputDir, $"{capture.CaptureId}{ext}");
                    if (!File.Exists(filePath) || new FileInfo(filePath).Length != capture.SizeInBytes)
                    {
                        pending.Add(capture);
                    }
                }

                if (pending.Count > 0)
                {
                    var options = new DownloadOptions
                    {
                        OutputDirectory = outputDir,
                        MaxConcurrent = 3,
                        SkipExisting = false,
                    };

                    var downloadManager = new DownloadManager(mediaClient, options);
                    var completed = 0;
                    var progress = new Progress<DownloadProgress>(p =>
                    {
                        if (p.State == DownloadState.Completed || p.State == DownloadState.Skipped)
                        {
                            var current = Interlocked.Increment(ref completed);
                            this.trayViewModel.UpdateToolTip($"FrameDrop — Syncing... {current}/{pending.Count}");
                        }
                    });

                    await downloadManager.DownloadCapturesAsync(pending, progress, ct);
                }

                this.trayViewModel.UpdateStatus($"Synced \u2014 {totalCaptures} captures", DateTimeOffset.Now);
                this.trayViewModel.UpdateToolTip($"FrameDrop \u2014 Synced \u2014 {totalCaptures} captures");

                var (_, _, gamertag) = this.authService.GetCachedAuthInfo();
                this.trayViewModel.UpdateAuthState(true, gamertag);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                this.trayViewModel.UpdateStatus("Sync failed", null);
            }
            finally
            {
                this.trayViewModel.UpdateSyncingState(false);
            }
        }
    }
}
