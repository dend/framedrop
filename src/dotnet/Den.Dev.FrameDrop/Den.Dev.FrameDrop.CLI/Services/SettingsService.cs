using System;
using System.IO;
using System.Text.Json;
using Den.Dev.FrameDrop.CLI.Models;
using Den.Dev.FrameDrop.Models;

namespace Den.Dev.FrameDrop.CLI.Services
{
    /// <summary>
    /// Manages loading and saving of CLI application settings.
    /// </summary>
    public class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly string settingsPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        /// <param name="settingsPath">Path to the settings file. Defaults to <see cref="FrameDropConfiguration.DefaultSettingsPath"/>.</param>
        public SettingsService(string? settingsPath = null)
        {
            this.settingsPath = settingsPath ?? FrameDropConfiguration.DefaultSettingsPath;
        }

        /// <summary>
        /// Loads the application settings from disk.
        /// </summary>
        /// <returns>The loaded settings, or defaults if no settings file exists.</returns>
        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(this.settingsPath))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(this.settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch (Exception)
            {
                return new AppSettings();
            }
        }

        /// <summary>
        /// Saves the application settings to disk.
        /// </summary>
        /// <param name="settings">The settings to save.</param>
        public void Save(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(this.settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(this.settingsPath, json);
            }
            catch (Exception)
            {
                // Silent fail on I/O errors.
            }
        }
    }
}
