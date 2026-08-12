namespace UyduArayuz_1.Configuration;

public sealed class ApplicationSettings
{
    public BatteryGraphSettings BatteryGraph { get; init; } = new();
}

public sealed class BatteryGraphSettings
{
    public bool UseFixedPercentage { get; init; } = true;

    public double FixedPercentage { get; init; } = 90;
}
