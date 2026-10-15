using AsistenteIA.Models;
using AsistenteIA.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsistenteIA.ViewModels;

/// <summary>Pantalla Detalle: muestra el registro y permite actualizar verificación, categoría y notas (UPDATE).</summary>
public partial class DetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _database;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecord))]
    private ConversationRecord? record;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isVerified;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string category = "General";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string notes = string.Empty;

    [ObservableProperty] private string statusMessage = string.Empty;

    public IReadOnlyList<string> Categories => DatabaseService.Categories;
    public bool HasRecord => Record is not null;

    public DetailViewModel(DatabaseService database, IDialogService dialogs)
    {
        _database = database;
        _dialogs = dialogs;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out int id))
            await LoadAsync(id);
    }

    private async Task LoadAsync(int id)
    {
        Record = await _database.GetByIdAsync(id);
        if (Record is null)
        {
            StatusMessage = "La conversación ya no existe.";
            return;
        }
        IsVerified = Record.IsVerified;
        Category = Record.Category;
        Notes = Record.Notes;
        StatusMessage = string.Empty;
    }

    private bool HasChanges() =>
        Record is not null &&
        (IsVerified != Record.IsVerified || Category != Record.Category || Notes != Record.Notes);

    /// <summary>UPDATE del registro en SQLite.</summary>
    [RelayCommand(CanExecute = nameof(HasChanges))]
    private async Task SaveAsync()
    {
        if (Record is null) return;
        if (IsVerified && !Record.IsVerified && string.IsNullOrWhiteSpace(Notes))
        {
            bool ok = await _dialogs.ConfirmAsync("Marcar como verificada",
                "¿Contrastó esta respuesta con fuentes confiables? Se recomienda anotar la fuente en Notas.",
                "Sí, la verifiqué", "Cancelar");
            if (!ok) return;
        }

        Record.IsVerified = IsVerified;
        Record.Category = Category;
        Record.Notes = Notes.Trim();
        await _database.UpdateAsync(Record);
        OnPropertyChanged(nameof(Record));
        SaveCommand.NotifyCanExecuteChanged();
        StatusMessage = "Cambios guardados.";
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Record is null) return;
        bool ok = await _dialogs.ConfirmAsync("Eliminar conversación",
            "¿Desea eliminar definitivamente esta conversación?", "Eliminar", "Cancelar");
        if (!ok) return;
        await _database.DeleteAsync(Record);
        await _dialogs.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (Record is null) return;
        await Clipboard.Default.SetTextAsync($"Pregunta:\n{Record.Prompt}\n\nRespuesta:\n{Record.Answer}");
        StatusMessage = "Pregunta y respuesta copiadas.";
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (Record is null) return;
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Compartir conversación",
            Text = $"Pregunta: {Record.Prompt}\n\nRespuesta (generada con IA, {Record.VerificationText.ToLower()}):\n{Record.Answer}"
        });
    }
}
