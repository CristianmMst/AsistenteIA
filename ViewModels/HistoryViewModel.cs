using System.Collections.ObjectModel;
using AsistenteIA.Models;
using AsistenteIA.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsistenteIA.ViewModels;

/// <summary>Pantalla Historial: consulta, filtra, abre el detalle y elimina registros de SQLite (RF06).</summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedCategory = DatabaseService.CategoryAll;
    [ObservableProperty] private bool onlyVerified;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string summary = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    private bool isEmpty;

    public ObservableCollection<ConversationRecord> Records { get; } = [];

    public IReadOnlyList<string> CategoryFilters { get; } =
        [DatabaseService.CategoryAll, .. DatabaseService.Categories];

    public string EmptyMessage =>
        string.IsNullOrWhiteSpace(SearchText) && SelectedCategory == DatabaseService.CategoryAll && !OnlyVerified
            ? "Aún no hay conversaciones guardadas. Use Consultar IA y pulse Guardar."
            : "No hay resultados para el filtro aplicado.";

    public HistoryViewModel(DatabaseService database, IDialogService dialogs)
    {
        _database = database;
        _dialogs = dialogs;
    }

    partial void OnSearchTextChanged(string value) => LoadCommand.Execute(null);
    partial void OnSelectedCategoryChanged(string value) => LoadCommand.Execute(null);
    partial void OnOnlyVerifiedChanged(bool value) => LoadCommand.Execute(null);

    /// <summary>READ: consulta filtrada (texto, categoría, verificadas).</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _database.SearchAsync(SearchText, SelectedCategory, OnlyVerified);
            int total = await _database.CountAsync();
            Records.Clear();
            foreach (var item in items) Records.Add(item);
            IsEmpty = Records.Count == 0;
            Summary = $"{Records.Count} de {total} conversaciones";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenAsync(ConversationRecord? record) =>
        record is null
            ? Task.CompletedTask
            : _dialogs.GoToAsync(nameof(Views.DetailPage), new Dictionary<string, object> { ["id"] = record.Id });

    /// <summary>DELETE con confirmación.</summary>
    [RelayCommand]
    private async Task DeleteAsync(ConversationRecord? record)
    {
        if (record is null) return;
        string preview = record.Prompt.Length > 60 ? record.Prompt[..60] + "…" : record.Prompt;
        bool ok = await _dialogs.ConfirmAsync("Eliminar conversación",
            $"¿Desea eliminar definitivamente esta conversación?\n\n\"{preview}\"", "Eliminar", "Cancelar");
        if (!ok) return;

        await _database.DeleteAsync(record);
        await LoadAsync();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedCategory = DatabaseService.CategoryAll;
        OnlyVerified = false;
    }
}
