using AsistenteIA.Services;
using AsistenteIA.ViewModels;
using AsistenteIA.Views;
using Microsoft.Extensions.Logging;

namespace AsistenteIA;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // HttpClient reutilizable (una sola instancia evita agotar sockets).
        // El tiempo máximo real lo controla AiService con el valor configurable por el usuario.
        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(SettingsService.DefaultBaseUrl),
            Timeout = TimeSpan.FromSeconds(310)
        });

        // Servicios
        builder.Services.AddSingleton(Connectivity.Current);
        builder.Services.AddSingleton<ConnectivityService>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<ApiKeyService>();
        builder.Services.AddSingleton<ApiLogService>();
        builder.Services.AddSingleton<AiService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<ChatViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<DetailViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<TemplatesViewModel>();

        // Vistas
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<DetailPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<TemplatesPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        app.Services.GetRequiredService<ConnectivityService>().StartListening();
        return app;
    }
}
