using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Den.Dev.Conch.Authentication;

namespace Den.Dev.FrameDrop.Desktop.Views
{
    public partial class LoginWindow : Window
    {
        private readonly SISUSessionManager sessionManager;
        private TaskCompletionSource<string?>? codeCompletionSource;

        public event Action<bool>? LoginCompleted;

        // Parameterless constructor for Avalonia XAML loader (design-time only).
        public LoginWindow() : this(null!)
        {
        }

        public LoginWindow(SISUSessionManager sessionManager)
        {
            InitializeComponent();
            this.sessionManager = sessionManager;

            this.SubmitButton.Click += this.OnSubmitClick;
            this.CancelButton.Click += this.OnCancelClick;
            this.OAuthUrlText.PointerPressed += this.OnUrlClick;

            this.SubmitButton.IsEnabled = false;
            _ = this.RunLoginFlowAsync();
        }

        private async Task RunLoginFlowAsync()
        {
            try
            {
                this.StatusText.Text = "Initiating login...";

                var sessionInfo = await this.sessionManager.InitiateSISULoginAsync(CancellationToken.None);
                if (sessionInfo == null)
                {
                    this.StatusText.Text = "Failed to initiate login. Close and try again.";
                    return;
                }

                this.OAuthUrlText.Text = sessionInfo.OAuthUrl;
                this.SubmitButton.IsEnabled = true;
                this.StatusText.Text = string.Empty;

                // Open browser
                try
                {
                    Process.Start(new ProcessStartInfo(sessionInfo.OAuthUrl!) { UseShellExecute = true });
                }
                catch (Exception)
                {
                    // Browser open failed — user can still click the link
                }

                // Wait for user to enter the code
                this.codeCompletionSource = new TaskCompletionSource<string?>();
                var code = await this.codeCompletionSource.Task;

                if (string.IsNullOrWhiteSpace(code))
                {
                    this.Close();
                    return;
                }

                this.StatusText.Text = "Completing authentication...";
                this.SubmitButton.IsEnabled = false;
                this.CodeTextBox.IsEnabled = false;

                var cache = await this.sessionManager.CompleteSISULoginAsync(sessionInfo, code.Trim(), CancellationToken.None);
                if (cache != null)
                {
                    this.LoginCompleted?.Invoke(true);
                    this.Close();
                }
                else
                {
                    this.StatusText.Text = "Authentication failed. Check the code and try again.";
                    this.SubmitButton.IsEnabled = true;
                    this.CodeTextBox.IsEnabled = true;

                    // Allow retry with a new code completion source
                    _ = this.WaitForRetryAsync(sessionInfo);
                }
            }
            catch (Exception ex)
            {
                this.StatusText.Text = $"Error: {ex.Message}";
                this.SubmitButton.IsEnabled = false;
            }
        }

        private async Task WaitForRetryAsync(object sessionInfo)
        {
            // sessionInfo is the same instance from InitiateSISULoginAsync
            // (same code verifier, so it must be reused)
            this.codeCompletionSource = new TaskCompletionSource<string?>();
            var code = await this.codeCompletionSource.Task;

            if (string.IsNullOrWhiteSpace(code))
            {
                this.Close();
                return;
            }

            this.StatusText.Text = "Completing authentication...";
            this.SubmitButton.IsEnabled = false;
            this.CodeTextBox.IsEnabled = false;

            try
            {
                var cache = await this.sessionManager.CompleteSISULoginAsync(
                    (dynamic)sessionInfo, code.Trim(), CancellationToken.None);
                if (cache != null)
                {
                    this.LoginCompleted?.Invoke(true);
                    this.Close();
                }
                else
                {
                    this.StatusText.Text = "Authentication failed again. Close and try again.";
                }
            }
            catch (Exception ex)
            {
                this.StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void OnSubmitClick(object? sender, RoutedEventArgs e)
        {
            this.codeCompletionSource?.TrySetResult(this.CodeTextBox.Text);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            this.codeCompletionSource?.TrySetResult(null);
            this.LoginCompleted?.Invoke(false);
            this.Close();
        }

        private void OnUrlClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            var url = this.OAuthUrlText.Text;
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception)
                {
                    // Ignore
                }
            }
        }
    }
}
