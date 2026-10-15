using System.Collections.ObjectModel;

namespace AsistenteIA.Services;

/// <summary>
/// Registro en memoria de las últimas solicitudes HTTP (método, URL, código, duración, cuerpos JSON).
/// Sirve como evidencia del consumo REST. La cabecera Authorization se guarda enmascarada.
/// </summary>
public class ApiLogService
{
    private const int MaxEntries = 30;

    public ObservableCollection<ApiLogEntry> Entries { get; } = [];

    public ApiLogEntry? Last => Entries.FirstOrDefault();

    public void Add(ApiLogEntry entry) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries) Entries.RemoveAt(Entries.Count - 1);
        });

    public void Clear() => MainThread.BeginInvokeOnMainThread(Entries.Clear);
}

public class ApiLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Method { get; init; } = "POST";
    public string Url { get; init; } = string.Empty;
    public string AuthorizationMasked { get; init; } = string.Empty;
    public string RequestBody { get; init; } = string.Empty;
    public int? StatusCode { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string ResponseBody { get; init; } = string.Empty;
    public long ElapsedMs { get; init; }

    public string Summary =>
        $"{Timestamp:HH:mm:ss}  {Method} {StatusCode?.ToString() ?? "—"} {StatusText}  ({ElapsedMs} ms)";

    public string FullText =>
        $"{Method} {Url}\nAuthorization: Bearer {AuthorizationMasked}\nContent-Type: application/json\n\n{RequestBody}\n\n" +
        $"HTTP {StatusCode?.ToString() ?? "—"} {StatusText} — {ElapsedMs} ms\n\n{ResponseBody}";
}
