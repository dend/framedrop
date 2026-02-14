namespace Den.Dev.FrameDrop.Download
{
    /// <summary>
    /// Options for controlling the download behavior.
    /// </summary>
    public class DownloadOptions
    {
        /// <summary>
        /// Gets or sets the output directory for downloaded captures.
        /// </summary>
        public string OutputDirectory { get; set; } = "./captures";

        /// <summary>
        /// Gets or sets the maximum number of concurrent downloads.
        /// </summary>
        public int MaxConcurrent { get; set; } = 3;

        /// <summary>
        /// Gets or sets a value indicating whether to skip files that already exist in the output directory.
        /// </summary>
        public bool SkipExisting { get; set; } = true;
    }
}
