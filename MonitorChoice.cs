namespace DoubleSceensBlack;

internal sealed class MonitorChoice
{
    public string DeviceName { get; }
    public string DisplayName { get; }
    public System.Drawing.Rectangle Bounds { get; }

    public MonitorChoice(string deviceName, string displayName, System.Drawing.Rectangle bounds)
    {
        DeviceName = deviceName;
        DisplayName = displayName;
        Bounds = bounds;
    }
}
