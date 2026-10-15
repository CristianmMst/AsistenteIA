using AsistenteIA.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsistenteIA.ViewModels;

/// <summary>Pantalla Inicio: propósito, estado de la app, accesos rápidos y recomendaciones de uso.</summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private readonly ApiKeyService _keyService;
    private readonly ConnectivityService _connectivity;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private int totalConversations;
    [ObservableProperty] private int verifiedConversations;
    [ObservableProperty] private bool hasKey;
    [ObservableProperty] private bool isOnline;

    public string KeyText => HasKey ? "Clave configurada" : "Falta configurar la clave";
    public string NetworkText => IsOnline ? "Conectado a internet" : "Sin conexión";

    public IReadOnlyList<string> Recommendations { get; } =
    [
        "Escriba preguntas académicas concretas y con contexto.",
        "No ingrese datos personales, contraseñas ni información confidencial.",
        "La IA puede equivocarse: contraste la respuesta con fuentes confiables.",
        "Marque como verificada solo la respuesta que ya contrastó.",
        "Declare el uso de IA en sus trabajos según las normas del curso."
    ];

    public HomeViewModel(DatabaseService database, ApiKeyService keyService,
        ConnectivityService connectivity, IDialogService dialogs)
    {
        _database = database;
        _keyService = keyService;
        _connectivity = connectivity;
        _dialogs = dialogs;
    }

    partial void OnHasKeyChanged(bool value) => OnPropertyChanged(nameof(KeyText));
    partial void OnIsOnlineChanged(bool value) => OnPropertyChanged(nameof(NetworkText));

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var all = await _database.GetAllAsync();
        TotalConversations = all.Count;
        VerifiedConversations = all.Count(r => r.IsVerified);
        HasKey = await _keyService.HasKeyAsync();
        IsOnline = _connectivity.IsOnline;
    }

    [RelayCommand]
    private Task GoAsync(string route) => _dialogs.GoToAsync($"//{route}");
}
