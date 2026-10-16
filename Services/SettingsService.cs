namespace AsistenteIA.Services;

/// <summary>Preferencias no sensibles: endpoint, modelo y tiempo de espera (configurables por cambio de proveedor).</summary>
public class SettingsService
{
    public const string DefaultBaseUrl = "https://gen.pollinations.ai/v1/";
    public const string DefaultModel = "openai/gpt-5.4-nano";
    public const int DefaultTimeoutSeconds = 60;

    public string BaseUrl
    {
        get => Preferences.Default.Get(nameof(BaseUrl), DefaultBaseUrl);
        set => Preferences.Default.Set(nameof(BaseUrl), Normalize(value));
    }

    public string Model
    {
        get => Preferences.Default.Get(nameof(Model), DefaultModel);
        set => Preferences.Default.Set(nameof(Model), string.IsNullOrWhiteSpace(value) ? DefaultModel : value.Trim());
    }

    public int TimeoutSeconds
    {
        get => Preferences.Default.Get(nameof(TimeoutSeconds), DefaultTimeoutSeconds);
        set => Preferences.Default.Set(nameof(TimeoutSeconds), Math.Clamp(value, 1, 300));
    }

    public void ResetToDefaults()
    {
        Preferences.Default.Remove(nameof(BaseUrl));
        Preferences.Default.Remove(nameof(Model));
        Preferences.Default.Remove(nameof(TimeoutSeconds));
    }

    private static string Normalize(string url)
    {
        url = string.IsNullOrWhiteSpace(url) ? DefaultBaseUrl : url.Trim();
        return url.EndsWith('/') ? url : url + "/";
    }
}
