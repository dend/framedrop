using System;
using System.IO;
using Den.Dev.Conch.Authentication;

namespace Den.Dev.FrameDrop.Models
{
    /// <summary>
    /// Configuration constants and paths for FrameDrop.
    /// </summary>
    public static class FrameDropConfiguration
    {
        /// <summary>
        /// The Xbox Live application ID.
        /// </summary>
        public const string AppId = "000000004424da1f";

        /// <summary>
        /// The redirect URI for OAuth authentication.
        /// </summary>
        public const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";

        /// <summary>
        /// The Xbox Live sandbox.
        /// </summary>
        public const string Sandbox = "RETAIL";

        /// <summary>
        /// The Xbox Live title ID.
        /// </summary>
        public const string TitleId = "704208617";

        /// <summary>
        /// The offers used for SISU authentication.
        /// </summary>
        public static readonly string[] Offers = new[] { "service::user.auth.xboxlive.com::MBI_SSL" };

        /// <summary>
        /// Gets the SISU application configuration for use with <see cref="SISUSessionManager"/>.
        /// </summary>
        public static SISUAppConfiguration AppConfiguration => new(
            AppId,
            TitleId,
            RedirectUri,
            Offers,
            Sandbox);

        /// <summary>
        /// Gets the base storage directory for FrameDrop data.
        /// </summary>
        public static string StorageDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Den.Dev",
            "FrameDrop");

        /// <summary>
        /// Gets the default path for the token cache file.
        /// </summary>
        public static string DefaultTokenCachePath => Path.Combine(StorageDirectory, "tokens.bin");

        /// <summary>
        /// Gets the default path for the settings file.
        /// </summary>
        public static string DefaultSettingsPath => Path.Combine(StorageDirectory, "settings.json");
    }
}
