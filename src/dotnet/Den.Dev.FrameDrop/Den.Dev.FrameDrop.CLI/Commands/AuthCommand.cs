using System;
using System.CommandLine;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Den.Dev.Conch.Authentication;
using Den.Dev.Conch.Storage;
using Den.Dev.FrameDrop.CLI.Services;
using Den.Dev.FrameDrop.Models;
using Spectre.Console;

namespace Den.Dev.FrameDrop.CLI.Commands
{
    /// <summary>
    /// Provides authentication-related CLI commands.
    /// </summary>
    public static class AuthCommand
    {
        /// <summary>
        /// Creates the "auth" command with login, status, and logout subcommands.
        /// </summary>
        /// <returns>The configured auth command.</returns>
        public static Command Create()
        {
            var authCommand = new Command("auth", "Manage Xbox Live authentication.");

            authCommand.AddCommand(CreateLoginCommand());
            authCommand.AddCommand(CreateStatusCommand());
            authCommand.AddCommand(CreateLogoutCommand());

            return authCommand;
        }

        private static Command CreateLoginCommand()
        {
            var verboseOption = new Option<bool>(
                "--verbose",
                getDefaultValue: () => false,
                description: "Show detailed HTTP request and response information.");

            var loginCommand = new Command("login", "Authenticate with Xbox Live using SISU.")
            {
                verboseOption,
            };

            loginCommand.SetHandler(async (bool verbose) =>
            {
                HttpClient? httpClient = null;
                if (verbose)
                {
                    httpClient = new HttpClient(new VerboseLoggingHandler());
                }

                var tokenStore = new EncryptedFileTokenStore(FrameDropConfiguration.DefaultTokenCachePath);
                var sessionManager = new SISUSessionManager(tokenStore, FrameDropConfiguration.AppConfiguration, httpClient);

                AnsiConsole.MarkupLine("[yellow]Attempting to restore previous session...[/]");
                var restored = await sessionManager.TryRestoreSessionAsync(CancellationToken.None);
                if (restored != null)
                {
                    AnsiConsole.MarkupLine($"[green]Session restored. Welcome back, {Markup.Escape(restored.Gamertag ?? "Unknown")}![/]");
                    AnsiConsole.MarkupLine($"[dim]XUID: {Markup.Escape(restored.XUID ?? "Unknown")}[/]");
                    return;
                }

                AnsiConsole.MarkupLine("[yellow]No valid session found. Starting SISU login...[/]");

                var sessionInfo = await sessionManager.InitiateSISULoginAsync(CancellationToken.None);
                if (sessionInfo == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed to initiate SISU session. Please try again.[/]");
                    return;
                }

                AnsiConsole.MarkupLine("[bold]Open the following URL in your browser to authenticate:[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[link]{Markup.Escape(sessionInfo.OAuthUrl!)}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]After authenticating, you will be redirected to a URL containing a code parameter.[/]");
                AnsiConsole.MarkupLine("[dim]Copy the value of the 'code' parameter from the redirect URL.[/]");
                AnsiConsole.WriteLine();

                var code = AnsiConsole.Ask<string>("[bold]Enter the authorization code:[/]");

                if (string.IsNullOrWhiteSpace(code))
                {
                    AnsiConsole.MarkupLine("[red]No authorization code provided.[/]");
                    return;
                }

                AnsiConsole.MarkupLine("[yellow]Completing authentication...[/]");

                var cache = await sessionManager.CompleteSISULoginAsync(sessionInfo, code.Trim(), CancellationToken.None);
                if (cache == null)
                {
                    AnsiConsole.MarkupLine("[red]Authentication failed. Please check the authorization code and try again.[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[green]Authentication successful! Welcome, {Markup.Escape(cache.Gamertag ?? "Unknown")}![/]");
                AnsiConsole.MarkupLine($"[dim]XUID: {Markup.Escape(cache.XUID ?? "Unknown")}[/]");
            }, verboseOption);

            return loginCommand;
        }

        private static Command CreateStatusCommand()
        {
            var statusCommand = new Command("status", "Show current authentication status.");

            statusCommand.SetHandler(() =>
            {
                var tokenStore = new EncryptedFileTokenStore(FrameDropConfiguration.DefaultTokenCachePath);
                var cache = tokenStore.Load();

                if (cache == null || string.IsNullOrEmpty(cache.XstsToken))
                {
                    AnsiConsole.MarkupLine("[yellow]Not authenticated. Run 'framedrop auth login' to authenticate.[/]");
                    return;
                }

                var table = new Table();
                table.AddColumn("Property");
                table.AddColumn("Value");

                table.AddRow("Gamertag", Markup.Escape(cache.Gamertag ?? "Unknown"));
                table.AddRow("XUID", Markup.Escape(cache.XUID ?? "Unknown"));
                table.AddRow("OAuth Expires", cache.OAuthExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                table.AddRow("XSTS Expires", cache.XstsExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));

                var oauthExpired = cache.OAuthExpiresAt < DateTimeOffset.UtcNow;
                var xstsExpired = cache.XstsExpiresAt < DateTimeOffset.UtcNow;

                table.AddRow("OAuth Status", oauthExpired ? "[red]Expired[/]" : "[green]Valid[/]");
                table.AddRow("XSTS Status", xstsExpired ? "[red]Expired[/]" : "[green]Valid[/]");

                AnsiConsole.Write(table);

                if (oauthExpired || xstsExpired)
                {
                    AnsiConsole.MarkupLine("[yellow]Tokens have expired. Run 'framedrop auth login' to refresh.[/]");
                }
            });

            return statusCommand;
        }

        private static Command CreateLogoutCommand()
        {
            var logoutCommand = new Command("logout", "Clear stored authentication tokens.");

            logoutCommand.SetHandler(() =>
            {
                var tokenStore = new EncryptedFileTokenStore(FrameDropConfiguration.DefaultTokenCachePath);
                tokenStore.Clear();

                AnsiConsole.MarkupLine("[green]Authentication tokens cleared.[/]");
            });

            return logoutCommand;
        }
    }
}
