using System.Collections.ObjectModel;
using AsistenteIA.Models;
using AsistenteIA.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsistenteIA.ViewModels;

public enum StatusKind { Info, Busy, Success, Error }

/// <summary>Pantalla Consultar IA: captura el prompt, aplica plantillas, consulta la API y guarda en SQLite.</summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly AiService _aiService;
    private readonly DatabaseService _database;
    private readonly SettingsService _settings;
    private readonly ConnectivityService _connectivity;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _cts;
    private string _answeredPrompt = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharCountText), nameof(IsOverLimit))]
    [NotifyCanExecuteChangedFor(nameof(AskCommand), nameof(ClearCommand))]
    private string prompt = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnswer))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string answer = string.Empty;

    [ObservableProperty] private string statusMessage = "Escriba una consulta o elija una plantilla.";
    [ObservableProperty] private StatusKind status = StatusKind.Info;
    [ObservableProperty] private string? validationMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AskCommand), nameof(CancelCommand), nameof(SaveCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isSaved;

    [ObservableProperty] private string selectedModel = SettingsService.DefaultModel;
    [ObservableProperty] private string selectedCategory = "General";
    [ObservableProperty] private PromptTemplate? selectedTemplate;
    [ObservableProperty] private bool isOffline;
    [ObservableProperty] private string resultInfo = string.Empty;

    public ObservableCollection<PromptTemplate> Templates { get; } = [];
    public IReadOnlyList<string> Categories => DatabaseService.Categories;

    public string CharCountText => $"{Prompt.Trim().Length} / {AiService.MaxPromptLength}";
    public bool IsOverLimit => Prompt.Trim().Length > AiService.MaxPromptLength;
    public bool HasAnswer => !string.IsNullOrWhiteSpace(Answer);

    public ChatViewModel(AiService aiService, DatabaseService database, SettingsService settings,
        ConnectivityService connectivity, IDialogService dialogs)
    {
        _aiService = aiService;
        _database = database;
        _settings = settings;
        _connectivity = connectivity;
        _dialogs = dialogs;
        _connectivity.StatusChanged += (_, online) => MainThread.BeginInvokeOnMainThread(() => IsOffline = !online);
    }

    /// <summary>Se ejecuta al mostrar la página: refresca modelo, plantillas y conectividad.</summary>
    [RelayCommand]
    private async Task AppearingAsync()
    {
        SelectedModel = _settings.Model;
        IsOffline = !_connectivity.IsOnline;

        int? selectedId = SelectedTemplate?.Id;
        Templates.Clear();
        foreach (var t in await _database.GetTemplatesAsync())
            Templates.Add(t);
        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == selectedId);
    }

    partial void OnSelectedTemplateChanged(PromptTemplate? value)
    {
        if (value is null) return;
        SelectedCategory = value.Category;
        // El texto de la plantilla queda editable; el usuario reemplaza {tema}.
        Prompt = value.Text;
        ValidationMessage = $"Reemplace {PromptTemplate.Placeholder} por su tema antes de consultar.";
    }

    private bool CanAsk() => !IsBusy && !string.IsNullOrWhiteSpace(Prompt);

    [RelayCommand(CanExecute = nameof(CanAsk))]
    private async Task AskAsync()
    {
        ValidationMessage = AiService.ValidatePrompt(Prompt);
        if (ValidationMessage is null && Prompt.Contains(PromptTemplate.Placeholder))
            ValidationMessage = $"Reemplace {PromptTemplate.Placeholder} por el tema de su consulta.";
        if (ValidationMessage is not null)
        {
            SetStatus(StatusKind.Error, "La consulta no se envió: revise el mensaje de validación.");
            return;
        }

        if (!_connectivity.IsOnline)
        {
            IsOffline = true;
            SetStatus(StatusKind.Error, "Sin conexión a internet. Conéctese y vuelva a intentar.");
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        IsSaved = false;
        Answer = string.Empty;
        ResultInfo = string.Empty;
        SetStatus(StatusKind.Busy, "Consultando el servicio de IA...");

        try
        {
            AiResult result = await _aiService.AskAsync(Prompt, SelectedModel, _cts.Token);
            Answer = result.Answer;
            _answeredPrompt = Prompt.Trim();
            ResultInfo = $"HTTP {result.StatusCode} · {result.Model} · {result.ElapsedMs} ms · {result.TotalTokens} tokens";
            SetStatus(StatusKind.Success, "Respuesta recibida. Verifique la información con fuentes confiables.");
        }
        catch (OperationCanceledException)
        {
            SetStatus(StatusKind.Info, "Consulta cancelada. Puede editarla y volver a enviarla.");
        }
        catch (AiServiceException ex)
        {
            SetStatus(StatusKind.Error, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(StatusKind.Error, ex.Message);
        }
        catch (ArgumentException ex)
        {
            ValidationMessage = ex.Message;
            SetStatus(StatusKind.Error, "La consulta no se envió.");
        }
        catch (Exception)
        {
            SetStatus(StatusKind.Error, "Ocurrió un error inesperado. Intente de nuevo.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    private bool CanSave() => HasAnswer && !IsBusy && !IsSaved;

    /// <summary>RF05: guarda la consulta y la respuesta en SQLite (INSERT).</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var record = new ConversationRecord
        {
            Prompt = _answeredPrompt,
            Answer = Answer,
            Category = SelectedCategory,
            Model = SelectedModel,
            CreatedAt = DateTime.UtcNow
        };
        await _database.SaveAsync(record);
        IsSaved = true;
        SetStatus(StatusKind.Success, $"Conversación guardada en el historial (Id {record.Id}).");
    }

    private bool CanClear() => !string.IsNullOrEmpty(Prompt) || HasAnswer;

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        Prompt = string.Empty;
        Answer = string.Empty;
        ResultInfo = string.Empty;
        ValidationMessage = null;
        SelectedTemplate = null;
        IsSaved = false;
        SetStatus(StatusKind.Info, "Escriba una consulta o elija una plantilla.");
    }

    [RelayCommand]
    private async Task CopyAnswerAsync()
    {
        if (!HasAnswer) return;
        await Clipboard.Default.SetTextAsync(Answer);
        SetStatus(StatusKind.Success, "Respuesta copiada al portapapeles.");
    }

    [RelayCommand]
    private Task ManageTemplatesAsync() => _dialogs.GoToAsync(nameof(Views.TemplatesPage));

    partial void OnPromptChanged(string value)
    {
        if (ValidationMessage is not null && AiService.ValidatePrompt(value) is null
            && !value.Contains(PromptTemplate.Placeholder))
            ValidationMessage = null;
    }

    private void SetStatus(StatusKind kind, string message)
    {
        Status = kind;
        StatusMessage = message;
    }
}
