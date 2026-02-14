using System.CommandLine;
using System.Threading.Tasks;
using Den.Dev.FrameDrop.CLI.Commands;

namespace Den.Dev.FrameDrop.CLI
{
    /// <summary>
    /// Entry point for the FrameDrop CLI application.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>The exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("FrameDrop - Download Xbox screenshots and video recordings.")
            {
                AuthCommand.Create(),
                ListCommand.Create(),
                DownloadCommand.Create(),
            };

            return await rootCommand.InvokeAsync(args);
        }
    }
}
