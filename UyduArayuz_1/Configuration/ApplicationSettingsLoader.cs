using System.IO;
using System.Text.Json;

namespace UyduArayuz_1.Configuration;

public static class ApplicationSettingsLoader
{
    private const string SettingsFileName = "appsettings.json";

    public static ApplicationSettings Load()
    {
        string settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            return new ApplicationSettings();
        }

        string json = File.ReadAllText(settingsPath);
        ApplicationSettings settings = JsonSerializer.Deserialize<ApplicationSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? throw new InvalidDataException($"{SettingsFileName} içeriği okunamadı.");

        Validate(settings);
        return settings;
    }

    private static void Validate(ApplicationSettings settings)
    {
        BatteryGraphSettings batteryGraph = settings.BatteryGraph
            ?? throw new InvalidDataException("BatteryGraph ayar bölümü null olamaz.");

        double fixedPercentage = batteryGraph.FixedPercentage;
        if (!double.IsFinite(fixedPercentage) || fixedPercentage is < 0 or > 100)
        {
            throw new InvalidDataException(
                $"BatteryGraph.FixedPercentage 0 ile 100 arasında olmalıdır. Girilen değer: {fixedPercentage}");
        }
    }
}
