namespace AsistenteIA.Services;

/// <summary>
/// Guarda la clave de la API en el almacenamiento seguro del sistema
/// (Android Keystore / Windows DPAPI). La clave nunca se escribe en el código ni en logs.
/// </summary>
public class ApiKeyService
{
    private const string KeyName = "pollinations_api_key";

    public Task SaveAsync(string key) =>
        SecureStorage.Default.SetAsync(KeyName, key.Trim());

    public async Task<string?> GetAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(KeyName);
        }
        catch (Exception)
        {
            // Si el almacén seguro se corrompe (p. ej. restauración de backup) se descarta la clave.
            SecureStorage.Default.Remove(KeyName);
            return null;
        }
    }

    public bool Remove() => SecureStorage.Default.Remove(KeyName);

    public async Task<bool> HasKeyAsync() => !string.IsNullOrWhiteSpace(await GetAsync());

    /// <summary>Representación segura para mostrar o registrar: solo prefijo y últimos 4 caracteres.</summary>
    public static string Mask(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "(sin clave)";
        key = key.Trim();
        string prefix = key.Length > 3 && key[2] == '_' ? key[..3] : string.Empty;
        string tail = key.Length > 8 ? key[^4..] : string.Empty;
        return $"{prefix}••••••••{tail}";
    }
}
