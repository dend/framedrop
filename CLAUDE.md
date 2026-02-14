# FrameDrop - Claude Code Project Guide

**Any changes to architecture, conventions, dependencies, or project structure must be documented in this file.**

## What This Project Is

FrameDrop is a cross-platform tool (Windows + Linux) for downloading Xbox screenshots and video recordings from Xbox cloud storage. Authentication uses the `Den.Dev.Conch` NuGet package which handles Xbox SISU authentication with device/title tokens.

## Solution Structure

```
src/dotnet/Den.Dev.FrameDrop/
├── Den.Dev.FrameDrop.sln
├── Den.Dev.FrameDrop/                  # Shared class library
│   ├── Authentication/
│   │   ├── FrameDropAuthenticationManager.cs   # Full SISU auth flow
│   │   ├── SISUSessionInfo.cs                 # Holds auth client instance across flow steps
│   │   └── TokenRefreshHandler.cs             # DelegatingHandler: auto-refresh on 401
│   ├── Storage/
│   │   ├── ITokenStore.cs
│   │   ├── TokenEncryptionHelper.cs           # AES-256-GCM encryption for token files
│   │   └── TokenStore.cs                      # Encrypted file-based token persistence
│   ├── Media/
│   │   ├── IXboxMediaClient.cs
│   │   └── XboxMediaClient.cs                 # Lists captures + downloads via content URIs
│   ├── Download/
│   │   ├── DownloadManager.cs                 # Concurrent downloads with SemaphoreSlim
│   │   ├── DownloadOptions.cs
│   │   └── DownloadResult.cs
│   └── Models/
│       ├── Capture.cs
│       ├── CaptureCollection.cs
│       ├── CaptureType.cs                     # Enum: Screenshot, Video
│       ├── FrameDropConfiguration.cs           # Auth constants + storage paths
│       └── FrameDropTokenCache.cs              # Flattened token cache for JSON serialization
└── Den.Dev.FrameDrop.CLI/              # Console app
    ├── Program.cs                             # System.CommandLine root command
    ├── Commands/
    │   ├── AuthCommand.cs                     # login (with --verbose), status, logout
    │   ├── ListCommand.cs                     # Stub - catches NotImplementedException
    │   └── DownloadCommand.cs                 # Stub - catches NotImplementedException
    ├── Services/
    │   ├── SettingsService.cs
    │   └── VerboseLoggingHandler.cs           # DelegatingHandler for HTTP debug logging
    └── Models/
        └── AppSettings.cs
```

## Build & Run

```bash
cd src/dotnet/Den.Dev.FrameDrop
dotnet build
dotnet run --project Den.Dev.FrameDrop.CLI -- auth login
dotnet run --project Den.Dev.FrameDrop.CLI -- auth login --verbose   # HTTP debug output
dotnet run --project Den.Dev.FrameDrop.CLI -- auth status
dotnet run --project Den.Dev.FrameDrop.CLI -- auth logout
dotnet run --project Den.Dev.FrameDrop.CLI -- list
dotnet run --project Den.Dev.FrameDrop.CLI -- download
```

## Conventions

- **Target framework:** `net10.0` (matches Conch)
- **ImplicitUsings:** `disable` (matches Conch convention)
- **Nullable:** `enable`
- **CLI framework:** `System.CommandLine` (2.0.0-beta4.22272.1)
- **Console UI:** `Spectre.Console` (0.49.1)
- **Auth library:** `Den.Dev.Conch` (0.0.2) from NuGet
- **CLI assembly name:** `framedrop` (so the executable is `framedrop.exe`/`framedrop`)

## Key Architecture Decisions

### SISU Auth Flow & Code Verifier Constraint
`XboxAuthenticationClient` generates a per-instance `codeVerifier`/`codeChallenge` pair. The **same instance** must be used for the entire auth flow (initiate session → user authenticates → complete login). This is why `SISUSessionInfo` holds an `internal` reference to the `AuthClient` instance.

### Token Storage
- Encrypted binary file at `%LOCALAPPDATA%\Den.Dev\FrameDrop\tokens.bin` (Windows) or `~/.local/share/Den.Dev/FrameDrop/tokens.bin` (Linux)
- **Encryption:** AES-256-GCM with PBKDF2-SHA256 key derivation (600,000 iterations)
- **Key material:** `Environment.MachineName + Environment.UserName` (machine-bound)
- **AAD:** `"framedrop-tokens-v1"` (UTF-8)
- **Binary format:** `[0x46 0x44]` magic + `0x01` version + 16-byte salt + 12-byte IV + 16-byte auth tag + ciphertext (47-byte header)
- **Fail-safe:** Any decryption/format error deletes the corrupt file and returns null
- `ITokenStore` interface allows substitution (in-memory for tests)
- Silent-fail on I/O errors

### HttpClient Passthrough
`FrameDropAuthenticationManager` accepts an optional `HttpClient` parameter, which is forwarded to `XboxAuthenticationClient`. This enables the `--verbose` flag on `auth login` — it injects a `VerboseLoggingHandler` (a `DelegatingHandler`) that logs every HTTP request/response to the console.

### Media Client
`XboxMediaClient.ListCapturesAsync` queries Xbox mediahub search APIs — `screenshots/search` for screenshots and `gameclips/search` for videos. When `captureType` is null, both endpoints are queried and results combined. Each response is parsed for `contentLocators` — the `Download` locator provides the direct content URI. `DownloadCaptureContentAsync` simply GETs that URI and returns the response stream.

### Automatic Token Refresh
`TokenRefreshHandler` is a `DelegatingHandler` in the shared library that intercepts 401 Unauthorized responses from Xbox APIs. On 401, it calls `FrameDropAuthenticationManager.TryRestoreSessionAsync` to refresh the OAuth/XSTS tokens, updates the request's `Authorization` header with the new XBL3.0 token, and retries the request once. A `SemaphoreSlim(1,1)` prevents concurrent refresh storms when multiple parallel downloads hit 401 simultaneously. The `list` and `download` commands use the `XboxMediaClient` constructor that accepts `authManager` + `tokenStore` to enable this behavior automatically.

### Auth Constants
```
AppId:       000000004424da1f
RedirectUri: https://login.live.com/oauth20_desktop.srf
Sandbox:     RETAIL
TitleId:     704208617
TokenType:   code
Offers:      ["service::user.auth.xboxlive.com::MBI_SSL"]
```

## What's Not Done Yet

- **Desktop app** — deferred until CLI is solid.
- **Tests** — no test project yet.
