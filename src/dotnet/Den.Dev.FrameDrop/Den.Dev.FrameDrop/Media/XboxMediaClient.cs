using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Den.Dev.Conch.Authentication;
using Den.Dev.FrameDrop.Models;

namespace Den.Dev.FrameDrop.Media
{
    /// <summary>
    /// Implementation of <see cref="IXboxMediaClient"/> that calls Xbox mediahub APIs.
    /// </summary>
    public class XboxMediaClient : IXboxMediaClient
    {
        private const string ScreenshotsSearchUrl = "https://mediahub.xboxlive.com/screenshots/search";
        private const string GameClipsSearchUrl = "https://mediahub.xboxlive.com/gameclips/search";
        private const string ContractVersion = "3";
        private const int PageSize = 500;

        private readonly HttpClient client;
        private readonly string xuid;

        /// <summary>
        /// Initializes a new instance of the <see cref="XboxMediaClient"/> class.
        /// </summary>
        /// <param name="xbl3Token">The XBL3.0 authorization token.</param>
        /// <param name="xuid">The Xbox User ID for the authenticated user.</param>
        /// <param name="httpClient">Optional HttpClient instance.</param>
        public XboxMediaClient(string xbl3Token, string xuid, HttpClient? httpClient = null)
        {
            this.client = httpClient ?? new HttpClient();
            this.xuid = xuid;
            this.client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("XBL3.0", xbl3Token.Replace("XBL3.0 ", string.Empty));
            this.client.DefaultRequestHeaders.Add("x-xbl-contract-version", ContractVersion);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XboxMediaClient"/> class with automatic token refresh on 401.
        /// </summary>
        /// <param name="xbl3Token">The XBL3.0 authorization token.</param>
        /// <param name="xuid">The Xbox User ID for the authenticated user.</param>
        /// <param name="sessionManager">The session manager used to refresh tokens.</param>
        /// <param name="tokenStore">The token store for persisting refreshed tokens.</param>
        public XboxMediaClient(string xbl3Token, string xuid, SISUSessionManager sessionManager, IXboxTokenStore tokenStore)
        {
            this.client = new HttpClient(new XboxTokenRefreshHandler(sessionManager, tokenStore));
            this.xuid = xuid;
            this.client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("XBL3.0", xbl3Token.Replace("XBL3.0 ", string.Empty));
            this.client.DefaultRequestHeaders.Add("x-xbl-contract-version", ContractVersion);
        }

        /// <inheritdoc/>
        public async Task<CaptureCollection> ListCapturesAsync(CaptureType captureType, int count = 500, string? continuationToken = null, CancellationToken cancellationToken = default)
        {
            var url = captureType == CaptureType.Video ? GameClipsSearchUrl : ScreenshotsSearchUrl;
            return await QueryEndpointAsync(url, captureType, count, continuationToken, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CaptureCollection> ListAllCapturesAsync(CaptureType? captureType = null, CancellationToken cancellationToken = default)
        {
            var result = new CaptureCollection();

            if (captureType == null || captureType == CaptureType.Screenshot)
            {
                await DrainEndpointAsync(ScreenshotsSearchUrl, CaptureType.Screenshot, result, cancellationToken);
            }

            if (captureType == null || captureType == CaptureType.Video)
            {
                await DrainEndpointAsync(GameClipsSearchUrl, CaptureType.Video, result, cancellationToken);
            }

            return result;
        }

        private async Task DrainEndpointAsync(string url, CaptureType type, CaptureCollection result, CancellationToken cancellationToken)
        {
            string? continuationToken = null;

            do
            {
                var page = await QueryEndpointAsync(url, type, PageSize, continuationToken, cancellationToken);
                result.Captures.AddRange(page.Captures);
                result.RawResponses.AddRange(page.RawResponses);
                result.TotalCount += page.Captures.Count;
                continuationToken = page.ContinuationToken;
            }
            while (!string.IsNullOrEmpty(continuationToken));
        }

        /// <inheritdoc/>
        public async Task<Stream> DownloadCaptureContentAsync(string uri, CancellationToken cancellationToken = default)
        {
            var response = await this.client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }

        private async Task<CaptureCollection> QueryEndpointAsync(string url, CaptureType type, int count, string? continuationToken, CancellationToken cancellationToken)
        {
            var queryBody = new Dictionary<string, object>
            {
                ["query"] = $"OwnerXuid eq {this.xuid}",
                ["max"] = count,
            };

            if (!string.IsNullOrEmpty(continuationToken))
            {
                queryBody["continuationToken"] = continuationToken;
            }

            var json = JsonSerializer.Serialize(queryBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await this.client.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = new CaptureCollection();
            result.RawResponses.Add(responseBody);

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("values", out var values))
            {
                foreach (var item in values.EnumerateArray())
                {
                    var capture = new Capture
                    {
                        CaptureType = type,
                    };

                    if (item.TryGetProperty("contentId", out var contentId))
                    {
                        capture.CaptureId = contentId.GetString();
                    }

                    if (item.TryGetProperty("ownerXuid", out var ownerXuid))
                    {
                        capture.XUID = ownerXuid.ToString();
                    }

                    if (item.TryGetProperty("titleName", out var titleName))
                    {
                        capture.TitleName = titleName.GetString();
                    }

                    // Screenshots use "captureDate", game clips use "uploadDate".
                    if (item.TryGetProperty("uploadDate", out var uploadDate))
                    {
                        if (DateTimeOffset.TryParse(uploadDate.GetString(), out var parsed))
                        {
                            capture.UploadDate = parsed;
                        }
                    }
                    else if (item.TryGetProperty("captureDate", out var captureDate))
                    {
                        if (DateTimeOffset.TryParse(captureDate.GetString(), out var parsed))
                        {
                            capture.UploadDate = parsed;
                        }
                    }

                    if (item.TryGetProperty("expirationDate", out var expirationDate))
                    {
                        if (DateTimeOffset.TryParse(expirationDate.GetString(), out var parsed))
                        {
                            capture.ExpirationDate = parsed;
                        }
                    }

                    if (item.TryGetProperty("contentLocators", out var locators))
                    {
                        foreach (var locator in locators.EnumerateArray())
                        {
                            var locatorType = locator.TryGetProperty("locatorType", out var lt) ? lt.GetString() : null;

                            if (locatorType == "Download")
                            {
                                if (locator.TryGetProperty("uri", out var uri))
                                {
                                    capture.ContentUri = uri.GetString();
                                }

                                if (locator.TryGetProperty("fileSize", out var fileSize))
                                {
                                    capture.SizeInBytes = fileSize.GetInt64();
                                }
                            }
                            else if (locatorType == "Thumbnail_Small" && capture.ThumbnailUri == null)
                            {
                                if (locator.TryGetProperty("uri", out var uri))
                                {
                                    capture.ThumbnailUri = uri.GetString();
                                }
                            }
                        }
                    }

                    result.Captures.Add(capture);
                }
            }

            if (root.TryGetProperty("continuationToken", out var contToken))
            {
                result.ContinuationToken = contToken.GetString();
            }

            return result;
        }
    }
}
