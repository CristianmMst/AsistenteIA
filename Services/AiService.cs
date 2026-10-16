using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsistenteIA.Models;
using Microsoft.Extensions.Logging;

namespace AsistenteIA.Services;

/// <summary>Consume la API REST de IA (POST /chat/completions con JSON y autenticación Bearer).</summary>
public class AiService
{
    public const int MinPromptLength = 5;
    public const int MaxPromptLength = 2000;

    public const string SystemPrompt =
        "Eres un asistente académico. Responde en español de forma clara y breve, " +
        "indica incertidumbre y no inventes fuentes. Usa texto plano: sin Markdown ni LaTeX.";

    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    private readonly HttpClient _client;
    private readonly ApiKeyService _keyService;
    private readonly SettingsService _settings;
    private readonly ConnectivityService _connectivity;
    private readonly ApiLogService _log;
    private readonly ILogger<AiService> _logger;

    public AiService(HttpClient client, ApiKeyService keyService, SettingsService settings,
        ConnectivityService connectivity, ApiLogService log, ILogger<AiService> logger)
    {
        _client = client;
        _keyService = keyService;
        _settings = settings;
        _connectivity = connectivity;
        _log = log;
        _logger = logger;
    }

    /// <summary>RF02: valida contenido y longitud. Devuelve null si es válido o el mensaje de error.</summary>
    public static string? ValidatePrompt(string? prompt)
    {
        string clean = prompt?.Trim() ?? string.Empty;
        if (clean.Length == 0) return "Escriba una consulta antes de enviar.";
        if (clean.Length < MinPromptLength) return $"La consulta es demasiado corta (mínimo {MinPromptLength} caracteres).";
        if (clean.Length > MaxPromptLength) return $"La consulta supera {MaxPromptLength} caracteres.";
        return null;
    }

    public async Task<AiResult> AskAsync(string prompt, string model, CancellationToken token = default)
    {
        string? validation = ValidatePrompt(prompt);
        if (validation is not null)
            throw new ArgumentException(validation);

        if (!_connectivity.IsOnline)
            throw new AiServiceException("Sin conexión a internet. Verifique la red e intente de nuevo.");

        string? apiKey = await _keyService.GetAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure la clave de la API en la pestaña Configuración.");

        var requestBody = new ChatRequest
        {
            Model = string.IsNullOrWhiteSpace(model) ? _settings.Model : model,
            Messages =
            [
                new ChatMessage { Role = "system", Content = SystemPrompt },
                new ChatMessage { Role = "user", Content = prompt.Trim() }
            ]
        };

        var url = new Uri(new Uri(_settings.BaseUrl), "chat/completions");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(requestBody);

        // Tiempo máximo configurable, combinado con la cancelación del usuario.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

        var sw = Stopwatch.StartNew();
        string requestJson = JsonSerializer.Serialize(requestBody, PrettyJson);
        int? status = null;
        string statusText = string.Empty;
        string responseText = string.Empty;

        try
        {
            using HttpResponseMessage response = await _client.SendAsync(request, linked.Token);
            status = (int)response.StatusCode;
            statusText = response.ReasonPhrase ?? response.StatusCode.ToString();
            responseText = await response.Content.ReadAsStringAsync(linked.Token);

            if (!response.IsSuccessStatusCode)
                throw BuildHttpError(response.StatusCode, responseText);

            ChatResponse? result = JsonSerializer.Deserialize<ChatResponse>(responseText);
            string? answer = result?.Choices.FirstOrDefault()?.Message.Content;

            return new AiResult(
                string.IsNullOrWhiteSpace(answer) ? "El servicio no devolvió contenido." : ToPlainText(answer),
                result?.Model ?? requestBody.Model,
                (int)response.StatusCode,
                sw.ElapsedMilliseconds,
                result?.Usage?.TotalTokens ?? 0);
        }
        // En Android, AndroidMessageHandler cierra el socket al cancelar y lanza WebException("Socket closed")
        // en lugar de OperationCanceledException; por eso se decide por el estado de los tokens.
        catch (Exception ex) when (ex is not AiServiceException
                                   && timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
        {
            statusText = "Tiempo agotado";
            throw new AiServiceException(
                $"Tiempo agotado: el servicio no respondió en {_settings.TimeoutSeconds} s. Puede reintentar.");
        }
        catch (Exception ex) when (ex is not AiServiceException && token.IsCancellationRequested)
        {
            statusText = "Cancelada por el usuario";
            throw new OperationCanceledException("Consulta cancelada por el usuario.", ex, token);
        }
        catch (System.Net.WebException ex)
        {
            statusText = $"Error de red: {ex.Message}";
            _logger.LogWarning(ex, "Fallo de red al llamar {Url}", url);
            throw new AiServiceException(
                "No fue posible contactar el servicio. Revise la conexión o intente más tarde.");
        }
        catch (HttpRequestException ex)
        {
            statusText = $"Error de red: {ex.InnerException?.Message ?? ex.Message}";
            _logger.LogWarning(ex, "Fallo de red al llamar {Url}", url);
            if (IsCertificateError(ex))
                throw new AiServiceException(
                    "La red actual intercepta la conexión segura (certificado no válido, p. ej. un portal Wi-Fi). " +
                    "Inicie sesión en el portal o use otra red.", ex.StatusCode);
            throw new AiServiceException(
                "No fue posible contactar el servicio. Revise la conexión o intente más tarde.", ex.StatusCode);
        }
        catch (JsonException)
        {
            statusText = "JSON inválido";
            throw new AiServiceException("La respuesta del servicio no tiene el formato esperado.");
        }
        finally
        {
            sw.Stop();
            // Nunca se registra la clave: solo URL, código, duración y modelo.
            _logger.LogInformation("POST {Url} -> {Status} {StatusText} en {Elapsed} ms (modelo {Model})",
                url, status, statusText, sw.ElapsedMilliseconds, requestBody.Model);
            _log.Add(new ApiLogEntry
            {
                Method = "POST",
                Url = url.ToString(),
                AuthorizationMasked = ApiKeyService.Mask(apiKey),
                RequestBody = requestJson,
                StatusCode = status,
                StatusText = statusText,
                ResponseBody = PrettyOrRaw(responseText),
                ElapsedMs = sw.ElapsedMilliseconds
            });
        }
    }

    /// <summary>Prueba de conexión: consulta breve con la clave guardada.</summary>
    public Task<AiResult> TestConnectionAsync(CancellationToken token = default) =>
        AskAsync("Responde únicamente con la palabra OK.", _settings.Model, token);

    /// <summary>Modelos de texto gratuitos publicados por el proveedor (GET /models, catálogo público).</summary>
    public async Task<IReadOnlyList<string>> GetTextModelsAsync(CancellationToken token = default)
    {
        var url = new Uri(new Uri(_settings.BaseUrl), "models");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        ModelListResponse? list = await _client.GetFromJsonAsync<ModelListResponse>(url, cts.Token);
        return list?.Data
            .Where(m => m.Category == "text" && !m.PaidOnly)
            .Select(m => m.Id)
            .OrderBy(id => id)
            .ToList() ?? [];
    }

    private static AiServiceException BuildHttpError(HttpStatusCode code, string body)
    {
        string? apiCode = null;
        try
        {
            apiCode = JsonSerializer.Deserialize<ApiErrorEnvelope>(body)?.Error?.Code;
        }
        catch (JsonException) { }

        int n = (int)code;
        string message = n switch
        {
            400 => "Solicitud inválida (400). Revise el modelo seleccionado o el contenido de la consulta.",
            401 => "Clave inválida o ausente (401). Actualícela en Configuración.",
            402 => "Saldo o presupuesto de la clave agotado (402).",
            403 => "La clave no tiene permiso para este modelo (403). Elija otro modelo.",
            404 => "Endpoint o modelo no encontrado (404). Revise la configuración.",
            422 => "El proveedor rechazó la consulta por política de contenido (422).",
            429 => "Demasiadas solicitudes (429). Espere un momento y reintente.",
            >= 500 => $"El servicio de IA presenta fallas ({n}). Intente más tarde.",
            _ => $"Error del servicio ({n})."
        };
        return new AiServiceException(message, code, apiCode);
    }

    /// <summary>La interfaz muestra texto plano: se quitan marcas de Markdown y LaTeX que algunos modelos agregan.</summary>
    public static string ToPlainText(string text)
    {
        string t = text.Replace("\r\n", "\n");
        t = Regex.Replace(t, @"\*\*(.+?)\*\*", "$1");
        t = Regex.Replace(t, @"__(.+?)__", "$1");
        t = Regex.Replace(t, @"^#{1,6}\s*", "", RegexOptions.Multiline);
        t = Regex.Replace(t, @"^\s*[-*]\s+", "• ", RegexOptions.Multiline);
        t = Regex.Replace(t, @"\\[()\[\]]", "");
        t = Regex.Replace(t, @"`{1,3}", "");
        return t.Trim();
    }

    private static bool IsCertificateError(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is System.Security.Authentication.AuthenticationException) return true;
            string m = e.Message;
            if (m.Contains("not verified", StringComparison.OrdinalIgnoreCase)
                || m.Contains("certificate", StringComparison.OrdinalIgnoreCase)
                || m.Contains("SSL", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string PrettyOrRaw(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(sin cuerpo)";
        try
        {
            using var doc = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(doc.RootElement, PrettyJson);
        }
        catch (JsonException)
        {
            return text.Length > 2000 ? text[..2000] + "…" : text;
        }
    }
}

public record AiResult(string Answer, string Model, int StatusCode, long ElapsedMs, int TotalTokens);
