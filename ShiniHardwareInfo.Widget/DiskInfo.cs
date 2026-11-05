using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShiniHardwareInfo.Widget;

public sealed class DiskInfo : INotifyPropertyChanged
{
    private string _name = "";
    private double _totalGb;
    private double _freeGb;
    private double _temp;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public double TotalGb { get => _totalGb; set { _totalGb = value; OnPropertyChanged(); OnPropertyChanged(nameof(UsedGb)); OnPropertyChanged(nameof(LoadPercent)); } }
    public double FreeGb { get => _freeGb; set { _freeGb = value; OnPropertyChanged(); OnPropertyChanged(nameof(UsedGb)); OnPropertyChanged(nameof(LoadPercent)); } }
    public double UsedGb => Math.Max(0, TotalGb - FreeGb);
    public double LoadPercent => TotalGb > 0 ? (UsedGb / TotalGb) * 100.0 : 0;
    public double Temp { get => _temp; set { _temp = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}