using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using Hwinfo.SharedMemory;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace ShiniHardwareInfo.Widget;

public sealed class HardwareMonitor : INotifyPropertyChanged, IDisposable
{
    private readonly Computer _computer;
    private readonly DispatcherTimer _timer;
    private readonly WidgetSettings? _settings;
    private readonly SharedMemoryReader? _hwReader;

    private string _cpuName = "CPU";
    private string _gpuName = "GPU";
    private double _cpuLoad;
    private double _cpuTemp;
    private double _cpuClock;
    private double _cpuVoltage;
    private double _ramLoad;
    private double _ramUsed;
    private double _ramTotal;
    private double _ramAvailable;
    private double _gpuLoad;
    private double _gpuTemp;
    private double _gpuClock;
    private double _gpuVoltage;
    private double _gpuMemUsed;
    private double _gpuMemTotal;
    private double _systemTemp;

    public string CpuName { get => _cpuName; private set { _cpuName = value; OnPropertyChanged(); } }
    public string GpuName { get => _gpuName; private set { _gpuName = value; OnPropertyChanged(); } }
    public double CpuLoad { get => _cpuLoad; private set { _cpuLoad = value; OnPropertyChanged(); } }
    public double CpuTemp { get => _cpuTemp; private set { _cpuTemp = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCpuTemp)); } }
    public bool HasCpuTemp => _cpuTemp > 0;
    public double SystemTemp { get => _systemTemp; private set { _systemTemp = value; OnPropertyChanged(); } }
    public double CpuClock { get => _cpuClock; private set { _cpuClock = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCpuClock)); } }
    public bool HasCpuClock => _cpuClock > 0;
    public double CpuVoltage { get => _cpuVoltage; private set { _cpuVoltage = value; OnPropertyChanged(); } }
    public double RamLoad { get => _ramLoad; private set { _ramLoad = value; OnPropertyChanged(); } }
    public double RamUsed { get => _ramUsed; private set { _ramUsed = value; OnPropertyChanged(); } }
    public double RamTotal { get => _ramTotal; private set { _ramTotal = value; OnPropertyChanged(); } }
    public double RamAvailable { get => _ramAvailable; private set { _ramAvailable = value; OnPropertyChanged(); } }
    public ObservableCollection<DiskInfo> Disks { get; } = new();
    public double GpuLoad { get => _gpuLoad; private set { _gpuLoad = value; OnPropertyChanged(); } }
    public double GpuTemp { get => _gpuTemp; private set { _gpuTemp = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasGpuTemp)); } }
    public bool HasGpuTemp => _gpuTemp > 0;
    public double GpuClock { get => _gpuClock; private set { _gpuClock = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasGpuClock)); } }
    public bool HasGpuClock => _gpuClock > 0;
    public double GpuVoltage { get => _gpuVoltage; private set { _gpuVoltage = value; OnPropertyChanged(); } }
    public double GpuMemUsed { get => _gpuMemUsed; private set { _gpuMemUsed = value; OnPropertyChanged(); } }
    public double GpuMemTotal { get => _gpuMemTotal; private set { _gpuMemTotal = value; OnPropertyChanged(); } }

    public HardwareMonitor(WidgetSettings? settings = null)
    {
        _settings = settings;
        try { _hwReader = new SharedMemoryReader(); } catch { _hwReader = null; }
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
        try { _computer.Open(); } catch { }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Update();
    }

    public void Start() { Update(); _timer.Start(); }
    public void Stop() { _timer.Stop(); }

    private void Update()
    {
        try
        {
            _computer.Accept(new UpdateVisitor());

            double cpuLoad = 0, cpuTemp = 0, cpuClock = 0, cpuVoltage = 0;
            double ramLoad = 0, ramUsed = 0, ramAvail = 0;
            string cpuName = _cpuName;

            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType == HardwareType.Cpu)
                {
                    cpuName = hw.Name;
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load && s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                            cpuLoad = s.Value ?? cpuLoad;
                        if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature &&
                            (s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                             s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)))
                        {
                            if (s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || cpuTemp == 0)
                                cpuTemp = s.Value ?? cpuTemp;
                        }
                        if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock && s.Value.HasValue && s.Value.Value > 0 && cpuClock == 0)
                            cpuClock = s.Value.Value;
                        if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Voltage && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                            cpuVoltage = s.Value ?? cpuVoltage;
                    }
                }
                else if (hw.HardwareType == HardwareType.Memory)
                {
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load)
                            ramLoad = s.Value ?? ramLoad;
                        if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Data)
                        {
                            if (s.Name.Contains("Used", StringComparison.OrdinalIgnoreCase)) ramUsed = s.Value ?? ramUsed;
                            if (s.Name.Contains("Available", StringComparison.OrdinalIgnoreCase)) ramAvail = s.Value ?? ramAvail;
                        }
                    }
                }
                foreach (var sub in hw.SubHardware) { /* handled recursively below */ }
            }

            // recursive for subhardware (clocks sometimes nested)
            foreach (var hw in _computer.Hardware)
                CollectCpu(hw, ref cpuLoad, ref cpuTemp, ref cpuClock, ref cpuVoltage);

            // HWiNFO shared memory (works on HVCI systems)
            if (cpuTemp <= 0 && _hwReader != null)
            {
                try
                {
                    if (_hwReader.TryReadLocal(out var hw))
                    {
                        double best = 0;
                        foreach (var r in hw.Readings)
                        {
                            if (r.ReadingType != Hwinfo.SharedMemory.SensorType.Temp) continue;
                            var label = (r.LabelUser + " " + r.LabelOrig + " " + r.Sensor.NameUser).ToLowerInvariant();
                            if (!label.Contains("cpu") && !label.Contains("core") && !label.Contains("package")) continue;
                            if (r.Value > 0 && r.Value < 120 && r.Value > best) best = r.Value;
                        }
                        if (best > 0) cpuTemp = best;
                    }
                }
                catch { }
            }

            // WMI fallback for CPU temp if LHM couldn't read it
            if (cpuTemp <= 0)
                cpuTemp = ReadCpuTempWmi();

            // System thermal zone (always available, no admin needed)
            double systemTemp = ReadSystemTempWmi();

            double gpuLoad = 0, gpuTemp = 0, gpuClock = 0, gpuVoltage = 0;
            double gpuMemUsed = 0, gpuMemTotal = 0;
            string gpuName = _gpuName;

            var gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                   ?? _computer.Hardware.FirstOrDefault(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel);
            if (gpu is not null)
            {
                gpuName = gpu.Name;
                foreach (var s in gpu.Sensors)
                {
                    if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load)
                    {
                        if (s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && !s.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                            gpuLoad = s.Value ?? gpuLoad;
                    }
                    if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature)
                    {
                        if (s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || gpuTemp == 0)
                            gpuTemp = s.Value ?? gpuTemp;
                    }
                    if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock)
                    {
                        if (s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && gpuClock == 0)
                            gpuClock = s.Value ?? gpuClock;
                    }
                    if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Voltage && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        gpuVoltage = s.Value ?? gpuVoltage;
                    if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.SmallData)
                    {
                        if (s.Name.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase)) gpuMemUsed = s.Value ?? gpuMemUsed;
                        if (s.Name.Equals("GPU Memory Total", StringComparison.OrdinalIgnoreCase)) gpuMemTotal = s.Value ?? gpuMemTotal;
                    }
                }
                foreach (var sub in gpu.SubHardware)
                    ReadGpuSub(sub, ref gpuLoad, ref gpuTemp, ref gpuClock, ref gpuVoltage, ref gpuMemUsed, ref gpuMemTotal);
            }

            CpuName = cpuName;
            CpuLoad = cpuLoad;
            SystemTemp = systemTemp;
            CpuTemp = cpuTemp;
            CpuClock = cpuClock;
            CpuVoltage = cpuVoltage;
            RamLoad = ramLoad;
            RamUsed = ramUsed;
            RamTotal = ramUsed + ramAvail;
            RamAvailable = ramAvail;
            RefreshDisks();
            GpuName = gpuName;
            GpuLoad = gpuLoad;
            GpuTemp = gpuTemp;
            GpuClock = gpuClock;
            GpuVoltage = gpuVoltage;
            GpuMemUsed = gpuMemUsed;
            GpuMemTotal = gpuMemTotal;
        }
        catch { }
    }

    private static void CollectCpu(IHardware hw, ref double cpuLoad, ref double cpuTemp, ref double cpuClock, ref double cpuVoltage)
    {
        if (hw.HardwareType == HardwareType.Cpu)
        {
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock && s.Value.HasValue && s.Value.Value > 0 && cpuClock == 0)
                    cpuClock = s.Value.Value;
                if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Voltage && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                    cpuVoltage = s.Value ?? cpuVoltage;
            }
        }
        foreach (var sub in hw.SubHardware)
            CollectCpu(sub, ref cpuLoad, ref cpuTemp, ref cpuClock, ref cpuVoltage);
    }

    private static void ReadGpuSub(IHardware hw, ref double gpuLoad, ref double gpuTemp, ref double gpuClock, ref double gpuVoltage, ref double gpuMemUsed, ref double gpuMemTotal)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature && gpuTemp == 0)
                gpuTemp = s.Value ?? gpuTemp;
            if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock && gpuClock == 0 && s.Value.HasValue)
                gpuClock = s.Value.Value;
            if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Voltage && gpuVoltage == 0)
                gpuVoltage = s.Value ?? gpuVoltage;
            if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.SmallData)
            {
                if (s.Name.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase)) gpuMemUsed = s.Value ?? gpuMemUsed;
                if (s.Name.Equals("GPU Memory Total", StringComparison.OrdinalIgnoreCase)) gpuMemTotal = s.Value ?? gpuMemTotal;
            }
        }
        foreach (var sub in hw.SubHardware)
            ReadGpuSub(sub, ref gpuLoad, ref gpuTemp, ref gpuClock, ref gpuVoltage, ref gpuMemUsed, ref gpuMemTotal);
    }

    private static double ReadCpuTempWmi()
    {
        // Try MSAcpi thermal zones (works on many laptops/OEMs)
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            double best = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                var t = obj["CurrentTemperature"];
                if (t != null)
                {
                    double kelvin = Convert.ToDouble(t) / 10.0;
                    double celsius = kelvin - 273.15;
                    if (celsius > 0 && celsius < 120 && celsius > best) best = celsius;
                }
            }
            if (best > 0) return best;
        }
        catch { }
        // Try Win32_TemperatureProbe
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_TemperatureProbe");
            foreach (ManagementObject obj in searcher.Get())
            {
                var t = obj["CurrentReading"];
                if (t != null)
                {
                    double c = Convert.ToDouble(t);
                    if (c > 0 && c < 120) return c;
                }
            }
        }
        catch { }
        // Try CIM temperature sensors
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM CIM_TemperatureSensor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var t = obj["CurrentReading"];
                if (t != null)
                {
                    double c = Convert.ToDouble(t);
                    if (c > 0 && c < 120) return c;
                }
            }
        }
        catch { }
        return 0;
    }

    private static double ReadSystemTempWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\cimv2",
                "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
            foreach (ManagementObject obj in searcher.Get())
            {
                var t = obj["Temperature"];
                if (t != null)
                {
                    double v = Convert.ToDouble(t);
                    // perf counter is in tenths of degrees (e.g. 301 = 30.1 C)
                    double c = v / 10.0;
                    if (c > 0 && c < 120) return c;
                }
            }
        }
        catch { }
        return 0;
    }

    public void RefreshDisks()
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
            var hidden = _settings?.HiddenDisks ?? new List<string>();
            // remove missing or hidden
            for (int i = Disks.Count - 1; i >= 0; i--)
            {
                if (!drives.Any(d => d.Name == Disks[i].Name) || hidden.Contains(Disks[i].Name))
                    Disks.RemoveAt(i);
            }
            foreach (var d in drives)
            {
                if (hidden.Contains(d.Name)) continue;
                var existing = Disks.FirstOrDefault(x => x.Name == d.Name);
                if (existing == null)
                {
                    existing = new DiskInfo { Name = d.Name };
                    Disks.Add(existing);
                }
                existing.TotalGb = d.TotalSize / (1024.0 * 1024 * 1024);
                existing.FreeGb = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        try { _computer.Close(); } catch { }
        try { _hwReader?.Dispose(); } catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware) { hardware.Update(); foreach (var sub in hardware.SubHardware) sub.Accept(this); }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
