using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace Den.Dev.FrameDrop.CLI.Services
{
    /// <summary>
    /// HTTP delegating handler that logs request and response details to the console.
    /// </summary>
    public class VerboseLoggingHandler : DelegatingHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VerboseLoggingHandler"/> class.
        /// </summary>
        public VerboseLoggingHandler()
            : base(new HttpClientHandler())
        {
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[dim]───── REQUEST ─────[/]");
            AnsiConsole.MarkupLine($"[blue]{Markup.Escape(request.Method.ToString())}[/] [dim]{Markup.Escape(request.RequestUri?.ToString() ?? "")}[/]");

            foreach (var header in request.Headers)
            {
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(header.Key)}: {Markup.Escape(string.Join(", ", header.Value))}[/]");
            }

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(header.Key)}: {Markup.Escape(string.Join(", ", header.Value))}[/]");
                }

                var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(requestBody))
                {
                    AnsiConsole.MarkupLine("[dim]Body:[/]");
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(requestBody)}[/]");
                }
            }

            AnsiConsole.WriteLine();

            var response = await base.SendAsync(request, cancellationToken);

            AnsiConsole.MarkupLine("[dim]───── RESPONSE ─────[/]");
            AnsiConsole.MarkupLine($"[{(response.IsSuccessStatusCode ? "green" : "red")}]{Markup.Escape(((int)response.StatusCode).ToString())} {Markup.Escape(response.StatusCode.ToString())}[/]");

            foreach (var header in response.Headers)
            {
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(header.Key)}: {Markup.Escape(string.Join(", ", header.Value))}[/]");
            }

            foreach (var header in response.Content.Headers)
            {
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(header.Key)}: {Markup.Escape(string.Join(", ", header.Value))}[/]");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrEmpty(responseBody))
            {
                AnsiConsole.MarkupLine("[dim]Body:[/]");
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(responseBody)}[/]");
            }

            AnsiConsole.WriteLine();

            return response;
        }
    }
}
