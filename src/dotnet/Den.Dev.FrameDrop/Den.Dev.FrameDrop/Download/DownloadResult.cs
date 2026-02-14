namespace Den.Dev.FrameDrop.Download
{
    /// <summary>
    /// Represents the result of a single capture download.
    /// </summary>
    public class DownloadResult
    {
        /// <summary>
        /// Gets or sets the capture ID.
        /// </summary>
        public string? CaptureId { get; set; }

        /// <summary>
        /// Gets or sets the local file path where the capture was saved.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the download was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the download failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes downloaded.
        /// </summary>
        public long BytesDownloaded { get; set; }
    }
}
