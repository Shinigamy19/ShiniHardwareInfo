using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ShiniHardwareInfo.Widget;

public sealed class WidgetSettings : INotifyPropertyChanged
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShiniHardwareInfo", "settings.json");

    private bool _showRam = true;
    private bool _showRamExtended = false;
    private bool _showDisks = true;
    public List<string> HiddenDisks { get; set; } = new();
    private bool _showCpu = true;
    private bool _showGpu = true;
    private bool _showCpuTemp = true;
    private bool _showCpuClock = true;
    private bool _showCpuVoltage = false;
    private bool _showSystem = true;
    private bool _showGpuTemp = true;
    private bool _showGpuClock = false;
    private bool _showGpuVoltage = false;
    private bool _showGpuMemory = true;
    private bool _showNames = true;
    private double _windowWidth = 340;
    private double _windowHeight = 200;
    private double _fontScale = 1.0;

    public bool ShowRam { get => _showRam; set { _showRam = value; OnPropertyChanged(); } }
    public bool ShowRamExtended { get => _showRamExtended; set { _showRamExtended = value; OnPropertyChanged(); } }
    public bool ShowDisks { get => _showDisks; set { _showDisks = value; OnPropertyChanged(); } }
    public bool ShowCpu { get => _showCpu; set { _showCpu = value; OnPropertyChanged(); } }
    public bool ShowGpu { get => _showGpu; set { _showGpu = value; OnPropertyChanged(); } }
    public bool ShowCpuTemp { get => _showCpuTemp; set { _showCpuTemp = value; OnPropertyChanged(); } }
    public bool ShowCpuClock { get => _showCpuClock; set { _showCpuClock = value; OnPropertyChanged(); } }
    public bool ShowCpuVoltage { get => _showCpuVoltage; set { _showCpuVoltage = value; OnPropertyChanged(); } }
    public bool ShowSystem { get => _showSystem; set { _showSystem = value; OnPropertyChanged(); } }
    public bool ShowGpuTemp { get => _showGpuTemp; set { _showGpuTemp = value; OnPropertyChanged(); } }
    public bool ShowGpuClock { get => _showGpuClock; set { _showGpuClock = value; OnPropertyChanged(); } }
    public bool ShowGpuVoltage { get => _showGpuVoltage; set { _showGpuVoltage = value; OnPropertyChanged(); } }
    public bool ShowGpuMemory { get => _showGpuMemory; set { _showGpuMemory = value; OnPropertyChanged(); } }
    public bool ShowNames { get => _showNames; set { _showNames = value; OnPropertyChanged(); } }
    public double WindowWidth { get => _windowWidth; set { _windowWidth = value; OnPropertyChanged(); } }
    public double WindowHeight { get => _windowHeight; set { _windowHeight = value; OnPropertyChanged(); } }
    public double FontScale { get => _fontScale; set { _fontScale = value; OnPropertyChanged(); } }

    public static WidgetSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<WidgetSettings>(json) ?? new WidgetSettings();
            }
        }
        catch { }
        return new WidgetSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}