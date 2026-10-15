using System.Collections.ObjectModel;
using AsistenteIA.Models;
using AsistenteIA.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AsistenteIA.ViewModels;

/// <summary>Administración de plantillas de prompts académicos (RF07: plantillas editables).</summary>
public partial class TemplatesViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(EditorTitle))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private PromptTemplate? editing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string name = string.Empty;

    [ObservableProperty] private string category = "General";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string text = string.Empty;

    [ObservableProperty] private string statusMessage = string.Empty;

    public ObservableCollection<PromptTemplate> Templates { get; } = [];
    public IReadOnlyList<string> Categories => DatabaseService.Categories;
    public bool IsEditing => Editing is not null;
    public string EditorTitle => Editing?.Id > 0 ? "Editar plantilla" : "Nueva plantilla";

    public TemplatesViewModel(DatabaseService database, IDialogService dialogs)
    {
        _database = database;
        _dialogs = dialogs;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        Templates.Clear();
        foreach (var t in await _database.GetTemplatesAsync()) Templates.Add(t);
    }

    [RelayCommand]
    private void New() => Edit(new PromptTemplate { Text = $"Explica {PromptTemplate.Placeholder}" });

    [RelayCommand]
    private void Edit(PromptTemplate? template)
    {
        if (template is null) return;
        Editing = template;
        Name = template.Name;
        Category = template.Category;
        Text = template.Text;
        StatusMessage = string.Empty;
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && Text.Trim().Length >= AiService.MinPromptLength;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (Editing is null) return;
        Editing.Name = Name.Trim();
        Editing.Category = Category;
        Editing.Text = Text.Trim();
        await _database.SaveTemplateAsync(Editing);
        StatusMessage = Editing.Text.Contains(PromptTemplate.Placeholder)
            ? $"Plantilla \"{Editing.Name}\" guardada."
            : $"Guardada. Sugerencia: incluya {PromptTemplate.Placeholder} donde va el tema.";
        Editing = null;
        await LoadAsync();
    }

    [RelayCommand]
    private void CancelEdit() => Editing = null;

    private bool CanDelete() => Editing?.Id > 0;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (Editing is null) return;
        if (!await _dialogs.ConfirmAsync("Eliminar plantilla", $"¿Eliminar la plantilla \"{Editing.Name}\"?", "Eliminar"))
            return;
        await _database.DeleteTemplateAsync(Editing);
        Editing = null;
        StatusMessage = "Plantilla eliminada.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (!await _dialogs.ConfirmAsync("Restaurar plantillas",
                "Se reemplazarán todas las plantillas por las predeterminadas. ¿Continuar?", "Restaurar"))
            return;
        await _database.RestoreDefaultTemplatesAsync();
        Editing = null;
        StatusMessage = "Plantillas predeterminadas restauradas.";
        await LoadAsync();
    }
}
