using Den.Dev.FrameDrop.Models;

namespace Den.Dev.FrameDrop.Download
{
    /// <summary>
    /// Reports granular download progress for a single capture.
    /// </summary>
    public class DownloadProgress
    {
        /// <summary>
        /// Gets or sets the capture being downloaded.
        /// </summary>
        public Capture Capture { get; set; } = null!;

        /// <summary>
        /// Gets or sets the local file path for the download.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the current state of the download.
        /// </summary>
        public DownloadState State { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes downloaded so far.
        /// </summary>
        public long BytesDownloaded { get; set; }

        /// <summary>
        /// Gets or sets the total size of the content in bytes.
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the error message if the download failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents the state of a download operation.
    /// </summary>
    public enum DownloadState
    {
        /// <summary>Download is starting.</summary>
        Starting,

        /// <summary>Download is in progress.</summary>
        Downloading,

        /// <summary>Download completed successfully.</summary>
        Completed,

        /// <summary>Download was skipped (file already exists).</summary>
        Skipped,

        /// <summary>Download failed.</summary>
        Failed,
    }
}
