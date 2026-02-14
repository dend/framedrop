# Den.Dev.FrameDrop

A .NET library for listing and downloading Xbox screenshots and video recordings from Xbox cloud storage.

This is the shared library that powers the [FrameDrop CLI](../../README.md). It can also be used directly to build custom tools, services, or desktop apps that work with Xbox captures.

## Features

- **Capture listing** — query screenshots, videos, or both from Xbox mediahub APIs with automatic pagination
- **Concurrent downloads** — configurable parallelism with progress reporting and skip-existing support
- **Automatic token refresh** — HTTP handler intercepts 401 responses and refreshes tokens transparently
- **Encrypted token storage** — AES-256-GCM with PBKDF2-SHA256, machine-bound key derivation

## Dependencies

- [Den.Dev.Conch](https://www.nuget.org/packages/Den.Dev.Conch) — handles Xbox SISU authentication (device tokens, OAuth, XSTS)
- .NET 10 or later

## Namespaces

| Namespace | Purpose |
|---|---|
| `Den.Dev.FrameDrop.Media` | Listing and downloading captures from Xbox mediahub |
| `Den.Dev.FrameDrop.Download` | Concurrent download manager with progress reporting |
| `Den.Dev.FrameDrop.Authentication` | Manages the Conch auth flow and token refresh |
| `Den.Dev.FrameDrop.Storage` | Encrypted token persistence |
| `Den.Dev.FrameDrop.Models` | Shared models (Capture, CaptureType, token cache, configuration) |

## Usage

### Authentication

Before listing or downloading captures, you need an authenticated session. FrameDrop wraps [Den.Dev.Conch](https://www.nuget.org/packages/Den.Dev.Conch) to manage the Xbox SISU authentication flow and persist tokens between sessions.

> **Why the Xbox App client ID?** FrameDrop uses the Entra ID client application ID for the Xbox App for Windows (`000000004424da1f`). The mediahub APIs that serve screenshots and game clips require permissions that are not granted to third-party registered applications, so we reuse the first-party Xbox App identity.
>
> **Getting the authorization code.** When you open the OAuth URL in your browser and sign in, the browser will redirect to `https://login.live.com/oauth20_desktop.srf?code=<CODE>&...`. Because this is a desktop redirect URI, the page will likely show an error or go blank — that's expected. To grab the code, open your browser's developer tools (**F12**), switch to the **Network** tab, and look for the request to `oauth20_desktop.srf`. The `code` query parameter in that URL is what you paste back into FrameDrop.

```csharp
using Den.Dev.FrameDrop.Authentication;
using Den.Dev.FrameDrop.Storage;

var tokenStore = new TokenStore();
var authManager = new FrameDropAuthenticationManager(tokenStore);

// Try restoring a previous session from cached tokens.
var cache = await authManager.TryRestoreSessionAsync();

if (cache == null)
{
    // No valid session — start interactive login.
    var session = await authManager.InitiateSISULoginAsync();

    // The user opens session.OAuthUrl in a browser, signs in, and gets a code.
    Console.WriteLine($"Open this URL to sign in:\n{session.OAuthUrl}");
    Console.Write("Paste the authorization code: ");
    var code = Console.ReadLine();

    // Complete login. The same session object must be used because it
    // holds a code verifier that was generated when the flow started.
    cache = await authManager.CompleteSISULoginAsync(session, code);
}

Console.WriteLine($"Authenticated as {cache.Gamertag} ({cache.XUID})");
```

### Listing captures

`XboxMediaClient` queries the Xbox mediahub APIs for screenshots and video clips. Pass `authManager` and `tokenStore` to the constructor to enable automatic token refresh on 401 responses.

```csharp
using Den.Dev.FrameDrop.Media;
using Den.Dev.FrameDrop.Models;

var authHeader = FrameDropAuthenticationManager.GetAuthorizationHeaderValue(cache);
var mediaClient = new XboxMediaClient(authHeader, cache.XUID, authManager, tokenStore);

// List all captures (screenshots + videos), following pagination automatically.
var collection = await mediaClient.ListAllCapturesAsync();

foreach (var capture in collection.Captures)
{
    Console.WriteLine($"{capture.CaptureType}: {capture.TitleName} ({capture.SizeInBytes} bytes)");
}

// Or filter by type.
var screenshots = await mediaClient.ListAllCapturesAsync(CaptureType.Screenshot);
var videos = await mediaClient.ListAllCapturesAsync(CaptureType.Video);
```

### Downloading captures

`DownloadManager` handles concurrent downloads with optional progress reporting. Files that already exist on disk with the correct size are skipped, making re-runs safe.

```csharp
using Den.Dev.FrameDrop.Download;

var options = new DownloadOptions
{
    OutputDirectory = "./my-captures",
    MaxConcurrent = 5,
    SkipExisting = true,
};

var downloadManager = new DownloadManager(mediaClient, options);

var progress = new Progress<DownloadProgress>(p =>
{
    if (p.State == DownloadState.Downloading)
        Console.WriteLine($"Downloading {p.Capture.TitleName}: {p.BytesDownloaded}/{p.TotalBytes}");
});

var results = await downloadManager.DownloadCapturesAsync(collection.Captures, progress);

Console.WriteLine($"Downloaded {results.Count(r => r.Success)} captures");
```

Screenshots are saved as `.png` and videos as `.mp4`, named by capture ID.

### Custom HttpClient

You can pass a custom `HttpClient` to `FrameDropAuthenticationManager` to inject logging or other delegating handlers:

```csharp
var handler = new MyLoggingHandler { InnerHandler = new HttpClientHandler() };
var httpClient = new HttpClient(handler);

var authManager = new FrameDropAuthenticationManager(tokenStore, httpClient);
```

### Custom token storage

Implement `ITokenStore` to use a different storage backend (database, cloud, in-memory for tests):

```csharp
using Den.Dev.FrameDrop.Storage;

public class InMemoryTokenStore : ITokenStore
{
    private FrameDropTokenCache? _cache;

    public FrameDropTokenCache? Load() => _cache;
    public void Save(FrameDropTokenCache cache) => _cache = cache;
    public void Clear() => _cache = null;
}
```

## Token storage details

The default `TokenStore` encrypts tokens at rest using AES-256-GCM:

- **Key derivation:** PBKDF2-SHA256 with 600,000 iterations
- **Key material:** `MachineName + UserName` (machine-bound, not portable across machines)
- **AAD:** `"framedrop-tokens-v1"`
- **Binary format:** 2-byte magic (`FD`) + 1-byte version + 16-byte salt + 12-byte IV + 16-byte auth tag + ciphertext

**Storage path:**

| Platform | Path |
|---|---|
| Windows | `%LOCALAPPDATA%\Den.Dev\FrameDrop\tokens.bin` |
| Linux | `~/.local/share/Den.Dev/FrameDrop/tokens.bin` |

If the token file is corrupted or cannot be decrypted, it is automatically deleted and `Load()` returns `null`.

## API reference

### XboxMediaClient

| Method | Description |
|---|---|
| `ListCapturesAsync(type, count, continuationToken)` | List a single page of captures for the given type. |
| `ListAllCapturesAsync(type?)` | List all captures across all pages. Pass `null` for both screenshots and videos. |
| `DownloadCaptureContentAsync(uri)` | Download content from a capture's content URI. Returns a `Stream`. |

### DownloadManager

| Method | Description |
|---|---|
| `DownloadCapturesAsync(captures, progress?)` | Download captures concurrently. Reports progress via `IProgress<DownloadProgress>`. |

### FrameDropAuthenticationManager

| Method | Description |
|---|---|
| `TryRestoreSessionAsync()` | Refresh stored tokens via Conch and return the updated cache, or `null` if no valid session exists. |
| `InitiateSISULoginAsync()` | Start a new SISU login flow. Returns `SISUSessionInfo` containing the OAuth URL. |
| `CompleteSISULoginAsync(session, code)` | Complete login with an authorization code. Must use the same session from `InitiateSISULoginAsync`. |
| `GetAuthorizationHeaderValue(cache)` | Format an `XBL3.0 x={UserHash};{XstsToken}` header value. |
| `ClearStoredTokens()` | Delete all stored tokens. |

### Models

**`Capture`** — a single Xbox capture: `CaptureId`, `CaptureType`, `ContentUri`, `ThumbnailUri`, `UploadDate`, `ExpirationDate`, `TitleName`, `SizeInBytes`, `XUID`.

**`CaptureType`** — enum: `Screenshot`, `Video`.

**`CaptureCollection`** — paginated result: `Captures`, `ContinuationToken`, `TotalCount`.

**`DownloadOptions`** — configuration: `OutputDirectory`, `MaxConcurrent`, `SkipExisting`.

**`DownloadProgress`** — progress report: `Capture`, `FilePath`, `State`, `BytesDownloaded`, `TotalBytes`, `ErrorMessage`.

**`DownloadState`** — enum: `Starting`, `Downloading`, `Completed`, `Skipped`, `Failed`.

**`DownloadResult`** — outcome: `CaptureId`, `FilePath`, `Success`, `ErrorMessage`, `BytesDownloaded`.
