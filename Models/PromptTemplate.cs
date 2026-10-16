using SQLite;

namespace AsistenteIA.Models;

/// <summary>Plantilla de prompt académico editable (RF07). {tema} se reemplaza por el texto del usuario.</summary>
public class PromptTemplate
{
    public const string Placeholder = "{tema}";

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = "General";

    public string Text { get; set; } = string.Empty;

    public string Apply(string topic) =>
        Text.Contains(Placeholder) ? Text.Replace(Placeholder, topic.Trim()) : $"{Text}\n\n{topic.Trim()}";

    public override string ToString() => Name;
}
