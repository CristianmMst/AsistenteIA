using AsistenteIA.Models;
using SQLite;

namespace AsistenteIA.Services;

/// <summary>Persistencia local con SQLite: CRUD del historial y de las plantillas de prompts.</summary>
public class DatabaseService
{
    public const string DatabaseFileName = "asistenteia.db3";
    public const string CategoryAll = "Todas";

    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private SQLiteAsyncConnection? _db;

    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    public static IReadOnlyList<string> Categories { get; } =
        ["General", "Programación", "Matemáticas", "Redacción", "Investigación", "Ciencias"];

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_db is not null) return _db;

        await _initLock.WaitAsync();
        try
        {
            if (_db is null)
            {
                var db = new SQLiteAsyncConnection(DatabasePath, Flags);
                await db.CreateTableAsync<ConversationRecord>();
                await db.CreateTableAsync<PromptTemplate>();
                if (await db.Table<PromptTemplate>().CountAsync() == 0)
                    await db.InsertAllAsync(DefaultTemplates());
                _db = db;
            }
            return _db;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ---------- Historial (CRUD) ----------

    /// <summary>Crear (INSERT) si Id = 0; actualizar (UPDATE) en otro caso.</summary>
    public async Task<int> SaveAsync(ConversationRecord record)
    {
        var db = await GetConnectionAsync();
        return record.Id == 0 ? await db.InsertAsync(record) : await db.UpdateAsync(record);
    }

    public async Task<List<ConversationRecord>> GetAllAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<ConversationRecord>().OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    /// <summary>Consulta filtrada por texto (prompt o respuesta), categoría y estado de verificación.</summary>
    public async Task<List<ConversationRecord>> SearchAsync(string? text, string? category, bool onlyVerified)
    {
        var db = await GetConnectionAsync();
        var query = db.Table<ConversationRecord>();

        if (!string.IsNullOrWhiteSpace(category) && category != CategoryAll)
            query = query.Where(r => r.Category == category);
        if (onlyVerified)
            query = query.Where(r => r.IsVerified);
        if (!string.IsNullOrWhiteSpace(text))
        {
            string t = text.Trim();
            query = query.Where(r => r.Prompt.Contains(t) || r.Answer.Contains(t));
        }

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task<ConversationRecord?> GetByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<ConversationRecord>().Where(r => r.Id == id).FirstOrDefaultAsync();
    }

    public async Task<int> UpdateAsync(ConversationRecord record)
    {
        var db = await GetConnectionAsync();
        return await db.UpdateAsync(record);
    }

    public async Task<int> DeleteAsync(ConversationRecord record)
    {
        var db = await GetConnectionAsync();
        return await db.DeleteAsync(record);
    }

    public async Task<int> CountAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<ConversationRecord>().CountAsync();
    }

    // ---------- Plantillas (RF07) ----------

    public async Task<List<PromptTemplate>> GetTemplatesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<PromptTemplate>().OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<int> SaveTemplateAsync(PromptTemplate template)
    {
        var db = await GetConnectionAsync();
        return template.Id == 0 ? await db.InsertAsync(template) : await db.UpdateAsync(template);
    }

    public async Task<int> DeleteTemplateAsync(PromptTemplate template)
    {
        var db = await GetConnectionAsync();
        return await db.DeleteAsync(template);
    }

    public async Task RestoreDefaultTemplatesAsync()
    {
        var db = await GetConnectionAsync();
        await db.DeleteAllAsync<PromptTemplate>();
        await db.InsertAllAsync(DefaultTemplates());
    }

    private static List<PromptTemplate> DefaultTemplates() =>
    [
        new() { Name = "Explicar un concepto", Category = "General",
            Text = "Explica de forma sencilla, con un ejemplo, el siguiente concepto: {tema}" },
        new() { Name = "Resumir un texto", Category = "Redacción",
            Text = "Resume en máximo cinco viñetas las ideas principales del siguiente texto:\n{tema}" },
        new() { Name = "Revisar código C#", Category = "Programación",
            Text = "Revisa el siguiente código C#, explica qué hace e indica posibles errores o mejoras:\n{tema}" },
        new() { Name = "Preguntas de estudio", Category = "Investigación",
            Text = "Genera cinco preguntas de repaso, con su respuesta breve, sobre el tema: {tema}" },
        new() { Name = "Resolver paso a paso", Category = "Matemáticas",
            Text = "Resuelve paso a paso y justifica cada paso del siguiente ejercicio: {tema}" },
        new() { Name = "Comparar conceptos", Category = "General",
            Text = "Compara en una tabla breve (semejanzas y diferencias) los siguientes conceptos: {tema}" }
    ];
}
