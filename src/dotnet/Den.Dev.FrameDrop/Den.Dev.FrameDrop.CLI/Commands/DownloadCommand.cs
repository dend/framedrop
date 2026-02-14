using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading;
using Den.Dev.Conch.Authentication;
using Den.Dev.Conch.Storage;
using Den.Dev.FrameDrop.Download;
using Den.Dev.FrameDrop.Media;
using Den.Dev.FrameDrop.Models;
using Spectre.Console;

namespace Den.Dev.FrameDrop.CLI.Commands
{
    /// <summary>
    /// Provides the "download" CLI command for downloading Xbox captures.
    /// </summary>
    public static class DownloadCommand
    {
        private const int DescriptionWidth = 34; // "VID" + space + 30-char title

        /// <summary>
        /// Creates the "download" command.
        /// </summary>
        /// <returns>The configured download command.</returns>
        public static Command Create()
        {
            var typeOption = new Option<string>(
                "--type",
                getDefaultValue: () => "all",
                description: "Type of captures to download (screenshots, videos, all).");

            var outputOption = new Option<string>(
                "--output",
                getDefaultValue: () => "./captures",
                description: "Output directory for downloaded captures.");

            var countOption = new Option<int>(
                "--count",
                getDefaultValue: () => -1,
                description: "Maximum number of captures to download (-1 for all).");

            var parallelOption = new Option<int>(
                "--parallel",
                getDefaultValue: () => 3,
                description: "Maximum number of concurrent downloads.");

            var downloadCommand = new Command("download", "Download Xbox captures.")
            {
                typeOption,
                outputOption,
                countOption,
                parallelOption,
            };

            downloadCommand.SetHandler(async (string type, string output, int count, int parallel) =>
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

                CaptureType? captureTypeFilter = type.ToLowerInvariant() switch
                {
                    "screenshots" => CaptureType.Screenshot,
                    "videos" => CaptureType.Video,
                    _ => null,
                };

                var sessionManager = new SISUSessionManager(tokenStore, FrameDropConfiguration.AppConfiguration);
                var mediaClient = new XboxMediaClient(authHeader, cache.XUID, sessionManager, tokenStore);

                var typeLabel = captureTypeFilter switch
                {
                    CaptureType.Screenshot => "screenshots",
                    CaptureType.Video => "videos",
                    _ => "captures",
                };

                CaptureCollection allCaptures = null!;
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Fetching {typeLabel} from Xbox Live...", async ctx =>
                    {
                        allCaptures = await mediaClient.ListAllCapturesAsync(captureTypeFilter);
                    });

                var allCapturesList = allCaptures.Captures;
                AnsiConsole.MarkupLine($"[dim]Fetched {allCapturesList.Count} {typeLabel} from Xbox Live.[/]");

                if (allCapturesList.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No captures found.[/]");
                    return;
                }

                // Pre-filter: separate already-downloaded from pending.
                var pending = new List<Capture>();
                var alreadyDownloaded = 0;
                var alreadyDownloadedBytes = 0L;

                foreach (var capture in allCapturesList)
                {
                    var ext = capture.CaptureType == CaptureType.Screenshot ? ".png" : ".mp4";
                    var filePath = Path.Combine(output, $"{capture.CaptureId}{ext}");

                    if (File.Exists(filePath) && new FileInfo(filePath).Length == capture.SizeInBytes)
                    {
                        alreadyDownloaded++;
                        alreadyDownloadedBytes += capture.SizeInBytes;
                    }
                    else
                    {
                        pending.Add(capture);
                    }
                }

                if (alreadyDownloaded > 0)
                {
                    AnsiConsole.MarkupLine($"[dim]{alreadyDownloaded} capture(s) already on disk ({FormatSize(alreadyDownloadedBytes)}), skipping.[/]");
                }

                if (pending.Count == 0)
                {
                    AnsiConsole.MarkupLine("[green]All captures already downloaded.[/]");
                    return;
                }

                var totalSize = 0L;
                foreach (var c in pending)
                {
                    totalSize += c.SizeInBytes;
                }

                AnsiConsole.MarkupLine($"[bold]Downloading {pending.Count} capture(s)[/] [dim]({FormatSize(totalSize)}) to {Markup.Escape(output)}[/]");
                AnsiConsole.WriteLine();

                var options = new DownloadOptions
                {
                    OutputDirectory = output,
                    MaxConcurrent = parallel,
                    SkipExisting = false, // We already pre-filtered.
                };

                var downloadManager = new DownloadManager(mediaClient, options);

                var progressTasks = new Dictionary<string, ProgressTask>();
                var lockObj = new object();
                var completedCount = 0;
                var failedCount = 0;
                var downloadedBytes = 0L;

                await AnsiConsole.Progress()
                    .AutoClear(true)
                    .HideCompleted(true)
                    .Columns(
                        new TaskDescriptionColumn { Alignment = Justify.Left },
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new DownloadedColumn(),
                        new TransferSpeedColumn(),
                        new SpinnerColumn())
                    .StartAsync(async ctx =>
                    {
                        var overallTask = ctx.AddTask(
                            FormatOverall(0, 0, pending.Count, 0, totalSize),
                            maxValue: totalSize > 0 ? totalSize : 1);

                        var progress = new Progress<DownloadProgress>(p =>
                        {
                            var id = p.Capture.CaptureId ?? "unknown";
                            var typeLabel = p.Capture.CaptureType == CaptureType.Video ? "VID" : "IMG";
                            var title = TruncateTitle(p.Capture.TitleName ?? "Unknown", 30);
                            var description = $"[dim]{typeLabel}[/] {Markup.Escape(title)}";

                            lock (lockObj)
                            {
                                switch (p.State)
                                {
                                    case DownloadState.Starting:
                                        if (!progressTasks.ContainsKey(id))
                                        {
                                            var task = ctx.AddTask(description, maxValue: p.TotalBytes > 0 ? p.TotalBytes : 1);
                                            progressTasks[id] = task;
                                        }

                                        break;

                                    case DownloadState.Downloading:
                                        if (progressTasks.TryGetValue(id, out var dlTask))
                                        {
                                            var increment = p.BytesDownloaded - dlTask.Value;
                                            if (increment > 0)
                                            {
                                                dlTask.Increment(increment);
                                                overallTask.Increment(increment);
                                            }
                                        }

                                        overallTask.Description = FormatOverall(completedCount, failedCount, pending.Count, (long)overallTask.Value, totalSize);
                                        break;

                                    case DownloadState.Completed:
                                        if (progressTasks.TryGetValue(id, out var compTask))
                                        {
                                            var remaining = compTask.MaxValue - compTask.Value;
                                            if (remaining > 0)
                                            {
                                                overallTask.Increment(remaining);
                                            }

                                            compTask.Value = compTask.MaxValue;
                                            compTask.StopTask();
                                            progressTasks.Remove(id);
                                            completedCount++;
                                            downloadedBytes += p.BytesDownloaded;
                                        }

                                        overallTask.Description = FormatOverall(completedCount, failedCount, pending.Count, (long)overallTask.Value, totalSize);
                                        break;

                                    case DownloadState.Skipped:
                                        if (p.TotalBytes > 0)
                                        {
                                            overallTask.Increment(p.TotalBytes);
                                        }

                                        completedCount++;
                                        overallTask.Description = FormatOverall(completedCount, failedCount, pending.Count, (long)overallTask.Value, totalSize);
                                        break;

                                    case DownloadState.Failed:
                                        if (progressTasks.TryGetValue(id, out var failTask))
                                        {
                                            failTask.StopTask();
                                            progressTasks.Remove(id);
                                        }

                                        failedCount++;
                                        overallTask.Description = FormatOverall(completedCount, failedCount, pending.Count, (long)overallTask.Value, totalSize);
                                        break;
                                }
                            }
                        });

                        await downloadManager.DownloadCapturesAsync(pending, progress, CancellationToken.None);

                        overallTask.Value = overallTask.MaxValue;
                        overallTask.StopTask();
                    });

                // Final summary.
                var summaryParts = new List<string>();
                summaryParts.Add($"[green]{completedCount} downloaded[/] [dim]({FormatSize(downloadedBytes)})[/]");
                if (alreadyDownloaded > 0)
                {
                    summaryParts.Add($"[dim]{alreadyDownloaded} already on disk[/]");
                }

                if (failedCount > 0)
                {
                    summaryParts.Add($"[red]{failedCount} failed[/]");
                }

                AnsiConsole.MarkupLine($"[bold]Done.[/] {string.Join("[dim] \u2502 [/]", summaryParts)}");
            }, typeOption, outputOption, countOption, parallelOption);

            return downloadCommand;
        }

        private static string FormatOverall(int completed, int failed, int total, long bytesDownloaded, long totalBytes)
        {
            var done = completed + failed;
            var countPart = $"[bold green]{done}[/][dim]/{total}[/]";
            var sizePart = $"[bold cyan]{FormatSize(bytesDownloaded)}[/][dim] / {FormatSize(totalBytes)}[/]";

            if (failed > 0)
            {
                return $"  {countPart}  [dim]\u2502[/]  {sizePart}  [dim]\u2502[/]  [red]{failed} failed[/]";
            }

            return $"  {countPart}  [dim]\u2502[/]  {sizePart}";
        }

        private static string TruncateTitle(string title, int maxLength)
        {
            if (title.Length <= maxLength)
            {
                return title.PadRight(maxLength);
            }

            return title.Substring(0, maxLength - 1) + "\u2026";
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
