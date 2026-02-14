using System;

namespace Den.Dev.FrameDrop.Models
{
    /// <summary>
    /// Represents a single Xbox capture (screenshot or video).
    /// </summary>
    public class Capture
    {
        /// <summary>
        /// Gets or sets the unique identifier for the capture.
        /// </summary>
        public string? CaptureId { get; set; }

        /// <summary>
        /// Gets or sets the type of capture.
        /// </summary>
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Gets or sets the URI for the capture content.
        /// </summary>
        public string? ContentUri { get; set; }

        /// <summary>
        /// Gets or sets the URI for the capture thumbnail.
        /// </summary>
        public string? ThumbnailUri { get; set; }

        /// <summary>
        /// Gets or sets the date and time the capture was uploaded.
        /// </summary>
        public DateTimeOffset UploadDate { get; set; }

        /// <summary>
        /// Gets or sets the expiration date for the capture content.
        /// </summary>
        public DateTimeOffset? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the name of the title (game) the capture was taken in.
        /// </summary>
        public string? TitleName { get; set; }

        /// <summary>
        /// Gets or sets the size of the capture in bytes.
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets the XUID of the user who made the capture.
        /// </summary>
        public string? XUID { get; set; }
    }
}
