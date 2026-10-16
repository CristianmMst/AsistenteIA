using System.Collections.ObjectModel;
using AsistenteIA.Models;
using AsistenteIA.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsistenteIA.ViewModels;

/// <summary>Configuración y Acerca de: clave segura (RF08), modelo, endpoint, prueba de conexión y registro HTTP.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiKeyService _keyService;
    private readonly AiService _aiService;
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly ApiLogService _apiLog;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveKeyCommand))]
    private string apiKeyInput = string.Empty;

    [ObservableProperty] private string keyStatus = "(sin clave)";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveKeyCommand), nameof(TestConnectionCommand))]
    private bool hasKey;

    [ObservableProperty] private string? selectedModel;
    [ObservableProperty] private string baseUrl = SettingsService.DefaultBaseUrl;
    [ObservableProperty] private string timeoutSeconds = SettingsService.DefaultTimeoutSeconds.ToString();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand), nameof(LoadModelsCommand))]
    private bool isBusy;

    [ObservableProperty] private string connectionMessage = string.Empty;
    [ObservableProperty] private StatusKind connectionStatus = StatusKind.Info;
    [ObservableProperty] private bool showApiLog;

    public ObservableCollection<string> Models { get; } = [];
    public ObservableCollection<ApiLogEntry> ApiLog { get; } = [];

    public string AppVersion => $"Versión {AppInfo.Current.VersionString} (compilación {AppInfo.Current.BuildString})";
    public string DatabasePath => DatabaseService.DatabasePath;

    public SettingsViewModel(ApiKeyService keyService, AiService aiService, SettingsService settings,
        ApiLogService apiLog, IDialogService dialogs)
    {
        _keyService = keyService;
        _aiService = aiService;
        _settings = settings;
        _dialogs = dialogs;
        _apiLog = apiLog;
    }

    /// <summary>La página deja de estar visible: se suelta el evento del singleton.</summary>
    public void Detach() => _apiLog.Changed -= OnApiLogChanged;

    private void OnApiLogChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RefreshApiLog);

    private void RefreshApiLog()
    {
        ApiLog.Clear();
        foreach (var entry in _apiLog.Snapshot()) ApiLog.Add(entry);
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        BaseUrl = _settings.BaseUrl;
        TimeoutSeconds = _settings.TimeoutSeconds.ToString();
        if (Models.Count == 0) Models.Add(_settings.Model);
        SelectedModel = _settings.Model;
        _apiLog.Changed -= OnApiLogChanged;
        _apiLog.Changed += OnApiLogChanged;
        RefreshApiLog();
        await RefreshKeyStatusAsync();
    }

    private async Task RefreshKeyStatusAsync()
    {
        string? key = await _keyService.GetAsync();
        HasKey = !string.IsNullOrWhiteSpace(key);
        // Solo se muestra una versión enmascarada; la clave completa nunca vuelve a la interfaz.
        KeyStatus = HasKey ? $"Clave guardada: {ApiKeyService.Mask(key)}" : "No hay clave configurada.";
    }

    private bool CanSaveKey() => !string.IsNullOrWhiteSpace(ApiKeyInput);

    [RelayCommand(CanExecute = nameof(CanSaveKey))]
    private async Task SaveKeyAsync()
    {
        string key = ApiKeyInput.Trim();
        if (key.Length < 10 || key.Contains(' '))
        {
            await _dialogs.AlertAsync("Clave no válida", "La clave parece incompleta. Cópiela de nuevo desde el proveedor.");
            return;
        }
        if (key.StartsWith("sk_", StringComparison.Ordinal))
        {
            bool ok = await _dialogs.ConfirmAsync("Clave secreta",
                "Está guardando una clave secreta (sk_). Úsela solo si es su clave personal con presupuesto limitado. " +
                "Nunca la comparta ni la suba al repositorio. ¿Continuar?", "Guardar", "Cancelar");
            if (!ok) return;
        }

        await _keyService.SaveAsync(key);
        ApiKeyInput = string.Empty; // se limpia el campo para que la clave no permanezca en memoria de la vista
        await RefreshKeyStatusAsync();
        SetConnection(StatusKind.Success, "Clave guardada en el almacenamiento seguro del dispositivo.");
    }

    private bool CanRemoveKey() => HasKey;

    [RelayCommand(CanExecute = nameof(CanRemoveKey))]
    private async Task RemoveKeyAsync()
    {
        bool ok = await _dialogs.ConfirmAsync("Eliminar clave",
            "¿Desea eliminar la clave de este dispositivo? No podrá consultar la IA hasta registrar otra.",
            "Eliminar", "Cancelar");
        if (!ok) return;
        _keyService.Remove();
        await RefreshKeyStatusAsync();
        SetConnection(StatusKind.Info, "Clave eliminada.");
    }

    private bool CanTest() => HasKey && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestConnectionAsync()
    {
        SaveSettings();
        IsBusy = true;
        SetConnection(StatusKind.Busy, "Probando conexión...");
        try
        {
            AiResult r = await _aiService.TestConnectionAsync();
            SetConnection(StatusKind.Success,
                $"Conexión exitosa: HTTP {r.StatusCode} en {r.ElapsedMs} ms con {r.Model}. Respuesta: \"{Truncate(r.Answer, 40)}\"");
        }
        catch (Exception ex) when (ex is AiServiceException or InvalidOperationException or OperationCanceledException)
        {
            SetConnection(StatusKind.Error, $"Falló la conexión: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLoadModels() => !IsBusy;

    /// <summary>Actualiza la lista con los modelos de texto gratuitos que publica el proveedor.</summary>
    [RelayCommand(CanExecute = nameof(CanLoadModels))]
    private async Task LoadModelsAsync()
    {
        IsBusy = true;
        SetConnection(StatusKind.Busy, "Consultando modelos disponibles...");
        try
        {
            SaveSettings();
            var models = await _aiService.GetTextModelsAsync();
            string current = _settings.Model;
            Models.Clear();
            foreach (var m in models) Models.Add(m);
            if (!Models.Contains(current)) Models.Insert(0, current);
            SelectedModel = current;
            SetConnection(StatusKind.Success, $"{models.Count} modelos de texto disponibles.");
        }
        catch (Exception)
        {
            SetConnection(StatusKind.Error, "No fue posible obtener la lista de modelos. Revise la conexión.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedModelChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) _settings.Model = value;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            await _dialogs.AlertAsync("Endpoint inválido", "El endpoint debe ser una URL https:// válida.");
            return;
        }
        SaveSettings();
        SetConnection(StatusKind.Success, "Configuración guardada.");
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        if (!await _dialogs.ConfirmAsync("Restablecer", "¿Restablecer endpoint, modelo y tiempo de espera?", "Restablecer"))
            return;
        _settings.ResetToDefaults();
        await AppearingAsync();
        SetConnection(StatusKind.Info, "Valores predeterminados restablecidos.");
    }

    [RelayCommand]
    private Task ManageTemplatesAsync() => _dialogs.GoToAsync(nameof(Views.TemplatesPage));

    [RelayCommand]
    private Task OpenProviderAsync() => Launcher.Default.OpenAsync("https://enter.pollinations.ai");

    private void SaveSettings()
    {
        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            _settings.BaseUrl = BaseUrl;
        if (int.TryParse(TimeoutSeconds, out int seconds))
            _settings.TimeoutSeconds = seconds;
        TimeoutSeconds = _settings.TimeoutSeconds.ToString();
        BaseUrl = _settings.BaseUrl;
    }

    private void SetConnection(StatusKind kind, string message)
    {
        ConnectionStatus = kind;
        ConnectionMessage = message;
    }

    private static string Truncate(string s, int n) => s.Length > n ? s[..n] + "…" : s;
}
