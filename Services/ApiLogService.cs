namespace AsistenteIA.Services;

/// <summary>
/// Registro en memoria de las últimas solicitudes HTTP (método, URL, código, duración, cuerpos JSON).
/// Sirve como evidencia del consumo REST. La cabecera Authorization se guarda enmascarada.
/// Es un singleton: no expone colecciones enlazables para no retener vistas de ventanas ya destruidas;
/// cada ViewModel toma una copia con <see cref="Snapshot"/> y escucha <see cref="Changed"/> solo mientras su página está visible.
/// </summary>
public class ApiLogService
{
    private const int MaxEntries = 30;

    private readonly List<ApiLogEntry> _entries = [];
    private readonly Lock _lock = new();

    public event EventHandler? Changed;

    public IReadOnlyList<ApiLogEntry> Snapshot()
    {
        lock (_lock) return [.. _entries];
    }

    public void Add(ApiLogEntry entry)
    {
        lock (_lock)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries) _entries.RemoveAt(_entries.Count - 1);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
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
