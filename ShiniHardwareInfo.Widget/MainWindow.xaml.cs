using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ShiniHardwareInfo.Widget;

public partial class MainWindow : Window
{
    private readonly HardwareMonitor _monitor;
    private readonly WidgetSettings _settings;

    public MainWindow(HardwareMonitor monitor, WidgetSettings settings)
    {
        _monitor = monitor;
        _settings = settings;
        InitializeComponent();
        try
        {
            var ico = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            if (ico != null)
                Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(ico.Handle, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        catch { }
        DataContext = _monitor;
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        ApplyFontScale();
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WidgetSettings.FontScale))
                ApplyFontScale();
            _settings.Save();
        };

        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        _settings.PropertyChanged += (_, _) => _settings.Save();
    }

    private void ApplyFontScale()
    {
        RootBorder.LayoutTransform = new ScaleTransform(_settings.FontScale, _settings.FontScale);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.Save();
        _monitor.Dispose();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        AddToggle(menu, "Mostrar RAM", "ShowRam");
        AddToggle(menu, "Mostrar CPU", "ShowCpu");
        AddToggle(menu, "Mostrar GPU", "ShowGpu");
        AddToggle(menu, "Mostrar RAM extendida", "ShowRamExtended");
        AddToggle(menu, "Mostrar Discos", "ShowDisks");
        menu.Items.Add(new Separator());
        var disksMenu = new MenuItem { Header = "Discos a mostrar" };
        foreach (var disk in _monitor.Disks.ToList())
        {
            var name = disk.Name;
            var cb = new CheckBox { Content = name, Margin = new Thickness(4, 2, 4, 2), IsChecked = !_settings.HiddenDisks.Contains(name) };
            cb.Checked += (_, _) => { _settings.HiddenDisks.Remove(name); _settings.Save(); _monitor.RefreshDisks(); };
            cb.Unchecked += (_, _) => { if (!_settings.HiddenDisks.Contains(name)) _settings.HiddenDisks.Add(name); _settings.Save(); _monitor.RefreshDisks(); };
            disksMenu.Items.Add(new MenuItem { Header = cb, StaysOpenOnClick = true });
        }
        if (disksMenu.Items.Count == 0)
            disksMenu.Items.Add(new MenuItem { Header = "(sin discos detectados)", IsEnabled = false });
        menu.Items.Add(disksMenu);
        menu.Items.Add(new Separator());
        AddToggle(menu, "CPU: Temperatura", "ShowCpuTemp");
        AddToggle(menu, "CPU: Frecuencia", "ShowCpuClock");
        AddToggle(menu, "CPU: Voltaje", "ShowCpuVoltage");
        menu.Items.Add(new Separator());
        AddToggle(menu, "GPU: Temperatura", "ShowGpuTemp");
        AddToggle(menu, "GPU: Frecuencia", "ShowGpuClock");
        AddToggle(menu, "GPU: Voltaje", "ShowGpuVoltage");
        AddToggle(menu, "GPU: Memoria (VRAM)", "ShowGpuMemory");
        menu.Items.Add(new Separator());
        AddToggle(menu, "Mostrar nombres de hardware", "ShowNames");
        menu.Items.Add(new Separator());
        var bigger = new MenuItem { Header = "Aumentar tamaño" };
        bigger.Click += (_, _) => { _settings.FontScale = Math.Min(2.0, _settings.FontScale + 0.1); };
        var smaller = new MenuItem { Header = "Disminuir tamaño" };
        smaller.Click += (_, _) => { _settings.FontScale = Math.Max(0.7, _settings.FontScale - 0.1); };
        var reset = new MenuItem { Header = "Restablecer tamaño" };
        reset.Click += (_, _) => { _settings.FontScale = 1.0; };
        menu.Items.Add(bigger);
        menu.Items.Add(smaller);
        menu.Items.Add(reset);
        menu.Items.Add(new Separator());
        var exit = new MenuItem { Header = "Salir" };
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exit);

        menu.PlacementTarget = SettingsButton;
        menu.IsOpen = true;
    }

    private void AddToggle(ContextMenu menu, string header, string propertyName)
    {
        var cb = new CheckBox { Content = header, Margin = new Thickness(4, 2, 4, 2) };
        var binding = new Binding(propertyName)
        {
            Source = _settings,
            Mode = BindingMode.TwoWay
        };
        cb.SetBinding(CheckBox.IsCheckedProperty, binding);
        var item = new MenuItem { Header = cb, StaysOpenOnClick = true };
        menu.Items.Add(item);
    }
}