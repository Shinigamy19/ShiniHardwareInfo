using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace ShiniHardwareInfo.Widget;

public partial class App : Application
{
    private MainWindow? _window;
    private TaskbarIcon? _tray;
    private HardwareMonitor? _monitor;
    private WidgetSettings? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(ex.Exception.Message, "ShiniHardwareInfo - Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            ex.Handled = true;
        };
        base.OnStartup(e);

        _settings = WidgetSettings.Load();
        Application.Current.Resources["Settings"] = _settings;
        _monitor = new HardwareMonitor(_settings);
        _window = new MainWindow(_monitor, _settings);
        _monitor.Start();

        _tray = new TaskbarIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            ToolTipText = "ShiniHardwareInfo",
            Visibility = Visibility.Visible
        };
        _tray.TrayMouseDoubleClick += (_, _) => ToggleWindow();
        _tray.ToolTipText = "ShiniHardwareInfo - Doble clic para mostrar/ocultar";

        var menu = new System.Windows.Controls.ContextMenu();
        var showItem = new System.Windows.Controls.MenuItem { Header = "Mostrar / Ocultar" };
        showItem.Click += (_, _) => ToggleWindow();
        var startupItem = new System.Windows.Controls.MenuItem
        {
            Header = "Iniciar con Windows",
            IsCheckable = true,
            IsChecked = IsStartupEnabled()
        };
        startupItem.Click += (_, _) => SetStartup(startupItem.IsChecked);
        var exitItem = new System.Windows.Controls.MenuItem { Header = "Salir" };
        exitItem.Click += (_, _) => ExitApp();

        menu.Items.Add(showItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exitItem);
        _tray.ContextMenu = menu;

        _window.Show();
        try { _tray.ShowBalloonTip("ShiniHardwareInfo", "Widget activo", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info); } catch { }
    }

    private void ToggleWindow()
    {
        if (_window is null) return;
        if (_window.IsVisible) _window.Hide(); else { _window.Show(); _window.Activate(); }
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("ShiniHardwareInfo") is string;
        }
        catch { return false; }
    }

    private static void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null) return;
            if (enable)
                key.SetValue("ShiniHardwareInfo", $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue("ShiniHardwareInfo", false);
        }
        catch { }
    }

    private void ExitApp()
    {
        _settings?.Save();
        _monitor?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}