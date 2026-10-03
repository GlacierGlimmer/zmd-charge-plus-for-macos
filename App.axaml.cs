using System.IO.Pipes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using EndfieldChargePlus.Customization;
using EndfieldChargePlus.Diagnostics;
using EndfieldChargePlus.Settings;
using EndfieldChargePlus.Views;

namespace EndfieldChargePlus;
public partial class App : Application
{
    private HudWindow? _hud;
    private CustomHudRuntime? _runtime;
    private TrayIcon? _tray;
    private SettingsWindow? _settingsWindow;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private readonly CancellationTokenSource _activation = new();
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var settings = SettingsManager.Load();
            LocalizationManager.Initialize(settings.UiLanguage);
            try { StartupManager.Apply(settings.StartWithWindows); }
            catch (Exception ex) { AppLog.Error("Could not refresh launch at login.", ex); }
            _hud = new HudWindow();
            _hud.ApplySettings(settings);
            _runtime = new CustomHudRuntime(_hud);
            _runtime.ApplySettings(settings);
            _runtime.Start();
            if (!Program.IsAutoStart) desktop.MainWindow = CreateSettings(settings);
            SetupMenu();
            LocalizationManager.LanguageChanged += SetupMenu;
            StartActivationListener();
            _ = CheckUpdatesAsync();
            desktop.Exit += (_, _) =>
            {
                LocalizationManager.LanguageChanged -= SetupMenu;
                _activation.Cancel();
                _tray?.Dispose(); _runtime?.Dispose(); _hud?.Close();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
    private SettingsWindow CreateSettings(AppSettings settings)
    {
        var window = new SettingsWindow(settings, _hud!, _runtime!);
        _settingsWindow = window;
        window.Closed += (_, _) => { if (ReferenceEquals(_settingsWindow, window)) _settingsWindow = null; };
        return window;
    }
    internal void OpenSettings()
    {
        if (_desktop is null) return;
        if (_settingsWindow is not { IsVisible: true })
        {
            _desktop.MainWindow = CreateSettings(SettingsManager.Load());
            _settingsWindow!.Show();
        }
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }
    private void SetupMenu()
    {
        NativeMenu BuildMenu()
        {
            var menu = new NativeMenu();
            var settings = new NativeMenuItem(LocalizationManager.Text("设置", "Settings"));
            settings.Click += (_, _) => OpenSettings();
            var preview = new NativeMenuItem(LocalizationManager.Text("预览", "Preview"));
            preview.Click += (_, _) => { if (_runtime is not null) _ = _runtime.PreviewActiveAsync(); };
            var update = new NativeMenuItem(LocalizationManager.Text("检查更新", "Check Updates"));
            update.Click += async (_, _) => { OpenSettings(); await CheckUpdatesAsync(); };
            var exit = new NativeMenuItem(LocalizationManager.Text("退出", "Quit"));
            exit.Click += (_, _) => _desktop?.Shutdown();
            menu.Items.Add(settings); menu.Items.Add(preview); menu.Items.Add(update);
            menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(exit);
            return menu;
        }
        _tray?.Dispose();
        using var icon = AssetLoader.Open(new Uri("avares://EndfieldChargePlus/Assets/tray_bolt.png"));
        _tray = new TrayIcon { Icon = new WindowIcon(icon), ToolTipText = ProductInfo.Name, Menu = BuildMenu(), IsVisible = true };
        _tray.Clicked += (_, _) => OpenSettings();
        TrayIcon.SetIcons(this, new TrayIcons { _tray });
        NativeMenu.SetMenu(this, BuildMenu());
    }
    private async Task CheckUpdatesAsync()
    {
        var result = await SettingsWindow.CheckForUpdatesAsync();
        await Dispatcher.UIThread.InvokeAsync(() => _settingsWindow?.ApplyUpdateCheckResult(result));
    }
    private void StartActivationListener()
    {
        var token = _activation.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(Program.MacInstanceName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(token);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    var data = new byte[1];
                    if (await pipe.ReadAsync(data, timeout.Token) == 1 && data[0] == 1)
                        await Dispatcher.UIThread.InvokeAsync(OpenSettings);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    AppLog.Warn($"Activation listener: {ex.Message}");
                    try { await Task.Delay(500, token); } catch (OperationCanceledException) { break; }
                }
            }
        }, token);
    }
}
