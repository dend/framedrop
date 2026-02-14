using System;
using System.CommandLine;
using System.Text.Json;
using Den.Dev.Conch.Authentication;
using Den.Dev.Conch.Storage;
using Den.Dev.FrameDrop.Media;
using Den.Dev.FrameDrop.Models;
using Spectre.Console;

namespace Den.Dev.FrameDrop.CLI.Commands
{
    /// <summary>
    /// Provides the "list" CLI command for listing Xbox captures.
    /// </summary>
    public static class ListCommand
    {
        /// <summary>
        /// Creates the "list" command.
        /// </summary>
        /// <returns>The configured list command.</returns>
        public static Command Create()
        {
            var verboseOption = new Option<bool>(
                "--verbose",
                getDefaultValue: () => false,
                description: "Print raw JSON API responses for debugging.");

            var listCommand = new Command("list", "List Xbox captures.")
            {
                verboseOption,
            };

            listCommand.SetHandler(async (bool verbose) =>
            {
                var tokenStore = new EncryptedFileTokenStore(FrameDropConfiguration.DefaultTokenCachePath);
                var cache = tokenStore.Load();

                if (cache == null || string.IsNullOrEmpty(cache.XstsToken))
                {
                    AnsiConsole.MarkupLine("[red]Not authenticated. Run 'framedrop auth login' first.[/]");
                    return;
                }

                var authHeader = cache.AuthorizationHeaderValue;
                if (string.IsNullOrEmpty(authHeader))
                {
                    AnsiConsole.MarkupLine("[red]Invalid token cache. Run 'framedrop auth login' to re-authenticate.[/]");
                    return;
                }

                if (string.IsNullOrEmpty(cache.XUID))
                {
                    AnsiConsole.MarkupLine("[red]No XUID in token cache. Run 'framedrop auth login' to re-authenticate.[/]");
                    return;
                }

                var sessionManager = new SISUSessionManager(tokenStore, FrameDropConfiguration.AppConfiguration);
                var mediaClient = new XboxMediaClient(authHeader, cache.XUID, sessionManager, tokenStore);

                CaptureCollection allCaptures = null!;
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Fetching captures from Xbox Live...", async ctx =>
                    {
                        allCaptures = await mediaClient.ListAllCapturesAsync();
                    });

                var captures = allCaptures.Captures;
                AnsiConsole.MarkupLine($"[dim]Fetched {captures.Count} capture(s) from Xbox Live.[/]");
                AnsiConsole.WriteLine();

                if (verbose)
                {
                    foreach (var raw in allCaptures.RawResponses)
                    {
                        var formatted = JsonSerializer.Serialize(JsonDocument.Parse(raw).RootElement, new JsonSerializerOptions { WriteIndented = true });
                        AnsiConsole.WriteLine(formatted);
                    }

                    AnsiConsole.WriteLine();
                }

                var table = new Table();
                table.AddColumn("Type");
                table.AddColumn("Title");
                table.AddColumn("Uploaded");
                table.AddColumn("Expires");
                table.AddColumn("Time Left");
                table.AddColumn("Size");

                captures.Sort((a, b) => b.UploadDate.CompareTo(a.UploadDate));

                foreach (var capture in captures)
                {
                    table.AddRow(
                        capture.CaptureType == CaptureType.Video ? "Video" : "Screenshot",
                        Markup.Escape(capture.TitleName ?? "Unknown"),
                        capture.UploadDate.ToString("yyyy-MM-dd HH:mm"),
                        FormatExpiration(capture.ExpirationDate),
                        FormatTimeLeft(capture.ExpirationDate),
                        FormatSize(capture.SizeInBytes));
                }

                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine($"[bold]Total: {captures.Count} capture(s)[/]");
            }, verboseOption);

            return listCommand;
        }

        private static string FormatExpiration(DateTimeOffset? expiration)
        {
            if (expiration == null)
            {
                return "[dim]—[/]";
            }

            var remaining = expiration.Value - DateTimeOffset.UtcNow;
            var color = remaining.TotalDays switch
            {
                <= 0 => "red",
                <= 1 => "red",
                <= 3 => "yellow",
                <= 7 => "yellow",
                _ => "green",
            };

            return $"[{color}]{Markup.Escape(expiration.Value.ToString("yyyy-MM-dd HH:mm"))}[/]";
        }

        private static string FormatTimeLeft(DateTimeOffset? expiration)
        {
            if (expiration == null)
            {
                return "[dim]—[/]";
            }

            var remaining = expiration.Value - DateTimeOffset.UtcNow;

            if (remaining.TotalSeconds <= 0)
            {
                return "[red bold]EXPIRED[/]";
            }

            string text;
            if (remaining.TotalDays >= 1)
            {
                text = $"{(int)remaining.TotalDays}d {remaining.Hours}h";
            }
            else if (remaining.TotalHours >= 1)
            {
                text = $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            }
            else
            {
                text = $"{remaining.Minutes}m";
            }

            var color = remaining.TotalDays switch
            {
                <= 1 => "red",
                <= 3 => "yellow",
                <= 7 => "yellow",
                _ => "green",
            };

            return $"[{color}]{text}[/]";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }
            else if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }
            else if (bytes < 1024 * 1024 * 1024)
            {
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            }
            else
            {
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }
    }
}
