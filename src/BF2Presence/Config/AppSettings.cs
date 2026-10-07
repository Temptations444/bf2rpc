using System.Text.Json;
using System.Text.Json.Serialization;

namespace BF2Presence.Config;

public sealed class AppSettings
{
    public string DiscordClientId { get; set; } = "1557494636556320768";

    public int UpdateIntervalSeconds { get; set; } = 5;

    public List<string> ProcessNames { get; set; } = ["starwarsbattlefrontii"];

    public List<string> MenuLevelKeywords { get; set; } = ["FrontEnd"];

    public string DefaultImageKey { get; set; } = "swbf2";

    public ShowSettings Show { get; set; } = new();

    public bool SetupDone { get; set; }

    public static AppSettings Load(string path) => JsonFile.Load<AppSettings>(path);
}

public sealed class ShowSettings
{
    public bool Map { get; set; } = true;
    public bool Mode { get; set; } = true;
    public bool Kills { get; set; } = true;
    public bool Assists { get; set; } = true;
    public bool Deaths { get; set; } = true;
    public bool Score { get; set; } = true;
    public bool Spe { get; set; } = true;
    public bool ElapsedTime { get; set; } = true;
    public bool InMenus { get; set; } = true;
    public bool Abbreviate { get; set; } = true;
}

internal static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static T Load<T>(string path) where T : new()
    {
        if (!File.Exists(path)) return new T();
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
    }
}
