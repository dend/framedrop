using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Den.Dev.FrameDrop.Desktop.Services;
using Den.Dev.FrameDrop.Desktop.ViewModels;
using Den.Dev.FrameDrop.Desktop.Views;

namespace Den.Dev.FrameDrop.Desktop
{
    public partial class App : Application
    {
        private AuthService? authService;
        private TrayViewModel? trayViewModel;
        private SyncService? syncService;
        private IClassicDesktopStyleApplicationLifetime? desktopLifetime;
        private LoginWindow? activeLoginWindow;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktopLifetime = desktop;
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                authService = new AuthService();
                trayViewModel = new TrayViewModel();
                syncService = new SyncService(authService, trayViewModel);

                SetupTrayIcon();
                _ = InitializeAsync();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void SetupTrayIcon()
        {
            var trayIcon = new TrayIcon
            {
                ToolTipText = "FrameDrop",
                Icon = CreateTrayIcon(),
            };

            var menu = new NativeMenu();

            // Header
            menu.Items.Add(new NativeMenuItem("FrameDrop") { IsEnabled = false });
            menu.Items.Add(new NativeMenuItemSeparator());

            // Status lines
            trayViewModel!.StatusMenuItem = new NativeMenuItem("Starting...") { IsEnabled = false };
            menu.Items.Add(trayViewModel.StatusMenuItem);

            trayViewModel.LastSyncMenuItem = new NativeMenuItem("Last sync: Never") { IsEnabled = false };
            menu.Items.Add(trayViewModel.LastSyncMenuItem);

            menu.Items.Add(new NativeMenuItemSeparator());

            // Actions
            trayViewModel.SyncNowMenuItem = new NativeMenuItem("Sync Now");
            trayViewModel.SyncNowMenuItem.Click += OnSyncNowClick;
            menu.Items.Add(trayViewModel.SyncNowMenuItem);

            trayViewModel.PauseMenuItem = new NativeMenuItem("Pause Syncing");
            trayViewModel.PauseMenuItem.Click += OnPauseClick;
            menu.Items.Add(trayViewModel.PauseMenuItem);

            var openFolderItem = new NativeMenuItem("Open Captures Folder");
            openFolderItem.Click += OnOpenFolderClick;
            menu.Items.Add(openFolderItem);

            menu.Items.Add(new NativeMenuItemSeparator());

            // Auth
            trayViewModel.AuthMenuItem = new NativeMenuItem("Log In");
            trayViewModel.AuthMenuItem.Click += OnAuthClick;
            menu.Items.Add(trayViewModel.AuthMenuItem);

            menu.Items.Add(new NativeMenuItemSeparator());

            // Quit
            var quitItem = new NativeMenuItem("Quit");
            quitItem.Click += OnQuitClick;
            menu.Items.Add(quitItem);

            trayIcon.Menu = menu;
            trayViewModel!.TrayIcon = trayIcon;

            var icons = new TrayIcons { trayIcon };
            TrayIcon.SetIcons(this, icons);
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                var restored = await authService!.TryRestoreSessionAsync(CancellationToken.None);

                if (restored)
                {
                    var (_, _, gamertag) = authService.GetCachedAuthInfo();
                    trayViewModel!.UpdateAuthState(true, gamertag);
                    trayViewModel.UpdateStatus("Starting sync...", null);
                    syncService!.Start();
                }
                else
                {
                    trayViewModel!.UpdateStatus("Not authenticated", null);
                    trayViewModel.UpdateAuthState(false, null);
                }
            }
            catch (Exception)
            {
                trayViewModel!.UpdateStatus("Not authenticated", null);
                trayViewModel.UpdateAuthState(false, null);
            }
        }

        private async void OnSyncNowClick(object? sender, EventArgs e)
        {
            await syncService!.SyncOnceAsync();
        }

        private void OnPauseClick(object? sender, EventArgs e)
        {
            if (syncService!.IsPaused)
            {
                syncService.Resume();
                trayViewModel!.UpdatePauseState(false);
            }
            else
            {
                syncService.Pause();
                trayViewModel!.UpdatePauseState(true);
            }
        }

        private void OnOpenFolderClick(object? sender, EventArgs e)
        {
            var dir = syncService!.OutputDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }

        private async void OnAuthClick(object? sender, EventArgs e)
        {
            if (authService!.HasValidTokens())
            {
                // Log out
                syncService!.Stop();
                authService.Logout();
                trayViewModel!.UpdateStatus("Not authenticated", null);
                trayViewModel.UpdateAuthState(false, null);
            }
            else
            {
                // Log in — bring existing window to front if already open
                if (activeLoginWindow != null)
                {
                    activeLoginWindow.Activate();
                    return;
                }

                var sessionManager = authService.CreateSessionManager();
                activeLoginWindow = new LoginWindow(sessionManager);
                activeLoginWindow.LoginCompleted += OnLoginCompleted;
                activeLoginWindow.Closed += (s, args) => activeLoginWindow = null;
                activeLoginWindow.Show();
            }
        }

        private void OnLoginCompleted(bool success)
        {
            if (success)
            {
                var (_, _, gamertag) = authService!.GetCachedAuthInfo();
                trayViewModel!.UpdateAuthState(true, gamertag);
                trayViewModel.UpdateStatus("Starting sync...", null);
                syncService!.Start();
            }
        }

        private void OnQuitClick(object? sender, EventArgs e)
        {
            syncService?.Stop();
            trayViewModel?.Dispose();
            desktopLifetime?.Shutdown();
        }

        private static WindowIcon CreateTrayIcon()
        {
            var size = 32;
            var bitmap = new WriteableBitmap(
                new PixelSize(size, size),
                new Vector(96, 96));

            using (var fb = bitmap.Lock())
            {
                // Xbox green (#107C10) in BGRA format
                var pixel = new byte[] { 0x10, 0x7C, 0x10, 0xFF };
                var row = new byte[size * 4];
                for (int x = 0; x < size; x++)
                {
                    Buffer.BlockCopy(pixel, 0, row, x * 4, 4);
                }

                for (int y = 0; y < size; y++)
                {
                    Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
                }
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;
            return new WindowIcon(ms);
        }
    }
}
