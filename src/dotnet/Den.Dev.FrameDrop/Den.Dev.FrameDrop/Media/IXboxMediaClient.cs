using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Den.Dev.FrameDrop.Models;

namespace Den.Dev.FrameDrop.Media
{
    /// <summary>
    /// Interface for interacting with Xbox media (screenshots and videos).
    /// </summary>
    public interface IXboxMediaClient
    {
        /// <summary>
        /// Lists a single page of captures.
        /// </summary>
        /// <param name="captureType">Filter by capture type. Must be Screenshot or Video (not null).</param>
        /// <param name="count">Maximum number of captures to return per page.</param>
        /// <param name="continuationToken">Continuation token from a previous response for pagination.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of captures with an optional continuation token.</returns>
        Task<CaptureCollection> ListCapturesAsync(CaptureType captureType, int count = 500, string? continuationToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all captures across all pages, optionally filtered by type.
        /// </summary>
        /// <param name="captureType">Filter by capture type, or null for all types.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection containing all captures.</returns>
        Task<CaptureCollection> ListAllCapturesAsync(CaptureType? captureType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the content of a capture from the given URI.
        /// </summary>
        /// <param name="uri">The content URI of the capture.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A stream containing the capture content.</returns>
        Task<Stream> DownloadCaptureContentAsync(string uri, CancellationToken cancellationToken = default);
    }
}
