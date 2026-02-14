using System.Collections.Generic;

namespace Den.Dev.FrameDrop.Models
{
    /// <summary>
    /// Represents a collection of Xbox captures with pagination support.
    /// </summary>
    public class CaptureCollection
    {
        /// <summary>
        /// Gets or sets the list of captures.
        /// </summary>
        public List<Capture> Captures { get; set; } = new List<Capture>();

        /// <summary>
        /// Gets or sets the continuation token for paginated results.
        /// </summary>
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets the total count of captures available.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Gets or sets the raw JSON response bodies from the API, for debugging.
        /// </summary>
        public List<string> RawResponses { get; set; } = new List<string>();
    }
}
