namespace DoubleSceensBlack;

internal sealed class AppSettings
{
    public int BlackIntensity { get; set; } = 0;
    public bool UseSharedIntensity { get; set; } = true;
    public Dictionary<string, int> MonitorIntensities { get; set; } = [];
    public int HotkeyVirtualKey { get; set; } = 0x42;
    public int HotkeyModifiers { get; set; } = 0x0002 | 0x0001;
    public bool IsBlueTheme { get; set; } = true;
    public bool MinimizeOnClose { get; set; }
}
