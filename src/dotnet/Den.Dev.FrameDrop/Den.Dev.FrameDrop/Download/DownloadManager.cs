using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Den.Dev.FrameDrop.Media;
using Den.Dev.FrameDrop.Models;

namespace Den.Dev.FrameDrop.Download
{
    /// <summary>
    /// Manages concurrent downloading of Xbox captures.
    /// </summary>
    public class DownloadManager
    {
        private const int BufferSize = 81920;

        private readonly IXboxMediaClient mediaClient;
        private readonly DownloadOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadManager"/> class.
        /// </summary>
        /// <param name="mediaClient">The media client used to download captures.</param>
        /// <param name="options">Download options.</param>
        public DownloadManager(IXboxMediaClient mediaClient, DownloadOptions options)
        {
            this.mediaClient = mediaClient;
            this.options = options;
        }

        /// <summary>
        /// Downloads a list of captures concurrently.
        /// </summary>
        /// <param name="captures">The captures to download.</param>
        /// <param name="progress">Optional progress reporter for granular download updates.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of download results.</returns>
        public async Task<List<DownloadResult>> DownloadCapturesAsync(
            List<Capture> captures,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<DownloadResult>();
            var semaphore = new SemaphoreSlim(this.options.MaxConcurrent);
            var tasks = new List<Task>();

            if (!Directory.Exists(this.options.OutputDirectory))
            {
                Directory.CreateDirectory(this.options.OutputDirectory);
            }

            foreach (var capture in captures)
            {
                await semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var result = await this.DownloadSingleCaptureAsync(capture, progress, cancellationToken);
                        lock (results)
                        {
                            results.Add(result);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            return results;
        }

        private async Task<DownloadResult> DownloadSingleCaptureAsync(Capture capture, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
        {
            var extension = capture.CaptureType == CaptureType.Screenshot ? ".png" : ".mp4";
            var fileName = $"{capture.CaptureId}{extension}";
            var filePath = Path.Combine(this.options.OutputDirectory, fileName);

            if (this.options.SkipExisting && File.Exists(filePath))
            {
                progress?.Report(new DownloadProgress
                {
                    Capture = capture,
                    FilePath = filePath,
                    State = DownloadState.Skipped,
                    BytesDownloaded = 0,
                    TotalBytes = capture.SizeInBytes,
                });

                return new DownloadResult
                {
                    CaptureId = capture.CaptureId,
                    FilePath = filePath,
                    Success = true,
                    BytesDownloaded = 0,
                };
            }

            try
            {
                progress?.Report(new DownloadProgress
                {
                    Capture = capture,
                    FilePath = filePath,
                    State = DownloadState.Starting,
                    BytesDownloaded = 0,
                    TotalBytes = capture.SizeInBytes,
                });

                using var stream = await this.mediaClient.DownloadCaptureContentAsync(capture.ContentUri!, cancellationToken);
                using var fileStream = File.Create(filePath);

                var buffer = new byte[BufferSize];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    progress?.Report(new DownloadProgress
                    {
                        Capture = capture,
                        FilePath = filePath,
                        State = DownloadState.Downloading,
                        BytesDownloaded = totalRead,
                        TotalBytes = capture.SizeInBytes,
                    });
                }

                progress?.Report(new DownloadProgress
                {
                    Capture = capture,
                    FilePath = filePath,
                    State = DownloadState.Completed,
                    BytesDownloaded = totalRead,
                    TotalBytes = capture.SizeInBytes,
                });

                return new DownloadResult
                {
                    CaptureId = capture.CaptureId,
                    FilePath = filePath,
                    Success = true,
                    BytesDownloaded = totalRead,
                };
            }
            catch (Exception ex)
            {
                progress?.Report(new DownloadProgress
                {
                    Capture = capture,
                    FilePath = filePath,
                    State = DownloadState.Failed,
                    TotalBytes = capture.SizeInBytes,
                    ErrorMessage = ex.Message,
                });

                return new DownloadResult
                {
                    CaptureId = capture.CaptureId,
                    FilePath = filePath,
                    Success = false,
                    ErrorMessage = ex.Message,
                };
            }
        }
    }
}
