using System.Text.Json.Serialization;

namespace Den.Dev.FrameDrop.CLI.Models
{
    /// <summary>
    /// Application settings for the FrameDrop CLI.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the default output directory for downloads.
        /// </summary>
        [JsonPropertyName("output_directory")]
        public string OutputDirectory { get; set; } = "./captures";

        /// <summary>
        /// Gets or sets the default number of concurrent downloads.
        /// </summary>
        [JsonPropertyName("max_concurrent_downloads")]
        public int MaxConcurrentDownloads { get; set; } = 3;
    }
}
