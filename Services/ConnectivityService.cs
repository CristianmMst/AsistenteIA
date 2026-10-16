namespace AsistenteIA.Services;

/// <summary>Consulta el estado de red antes de enviar solicitudes (RF04: ausencia de conexión).</summary>
public class ConnectivityService(IConnectivity connectivity)
{
    public bool IsOnline => connectivity.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler<bool>? StatusChanged;

    public void StartListening() =>
        connectivity.ConnectivityChanged += (_, e) =>
            StatusChanged?.Invoke(this, e.NetworkAccess == NetworkAccess.Internet);
}
