using SQLite;

namespace AsistenteIA.Models;

/// <summary>Entidad local del historial (tabla ConversationRecord en SQLite).</summary>
public class ConversationRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Indexed]
    public string Category { get; set; } = "General";

    public bool IsVerified { get; set; }

    /// <summary>Modelo de IA que generó la respuesta (trazabilidad).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Notas del usuario: fuentes con las que contrastó la respuesta.</summary>
    public string Notes { get; set; } = string.Empty;

    [Ignore]
    public string CreatedAtLocal => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    [Ignore]
    public string VerificationText => IsVerified ? "Verificada" : "Sin verificar";
}
