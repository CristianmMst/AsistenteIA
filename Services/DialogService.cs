namespace AsistenteIA.Services;

/// <summary>Diálogos y navegación detrás de una abstracción para que los ViewModels no dependan de las vistas.</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept = "Sí", string cancel = "Cancelar");
    Task AlertAsync(string title, string message, string ok = "Aceptar");
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null);
}

public class DialogService : IDialogService
{
    private static Page? CurrentPage => Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Sí", string cancel = "Cancelar") =>
        CurrentPage?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);

    public Task AlertAsync(string title, string message, string ok = "Aceptar") =>
        CurrentPage?.DisplayAlertAsync(title, message, ok) ?? Task.CompletedTask;

    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null) =>
        parameters is null ? Shell.Current.GoToAsync(route) : Shell.Current.GoToAsync(route, parameters);
}
