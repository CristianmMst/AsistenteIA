using AsistenteIA.Views;

namespace AsistenteIA;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Rutas de páginas que no están en la barra de pestañas (navegación jerárquica).
        Routing.RegisterRoute(nameof(DetailPage), typeof(DetailPage));
        Routing.RegisterRoute(nameof(TemplatesPage), typeof(TemplatesPage));
    }
}
