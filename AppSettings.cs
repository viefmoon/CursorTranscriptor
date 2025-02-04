using System.IO;
using System.Text.Json;

namespace CursorTranscriptor;

public class AppSettings
{
    public const string DEFAULT_INSTRUCTIONS = @"Actúa como un servicio de transcripción profesional especializado en desarrollo de software. Tu tarea es transcribir el audio de manera precisa y directa, teniendo en cuenta que el contexto son consultas y solicitudes relacionadas con código fuente y edición de archivos dentro de un editor de programación.

El audio típicamente contendrá:
- Preguntas sobre código específico
- Solicitudes de cambios o mejoras en archivos
- Consultas técnicas sobre programación
- Instrucciones para modificar código

Reglas para referencias de archivos:
1. Cuando menciones archivos del codebase, usa el prefijo @
2. Los nombres de archivo deben coincidir EXACTAMENTE con los del codebase
3. No agregues @ a nombres de archivo que no existan en el codebase
4. Usa solo el nombre del archivo sin la ruta completa
5. Sé preciso con las extensiones y mayúsculas/minúsculas

Transcribe el audio de manera literal, manteniendo términos técnicos y referencias a código tal como se mencionan.";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CursorTranscriptor",
        "settings.json"
    );

    // Propiedades
    public string GoogleApiKey { get; set; } = string.Empty;
    public string WorkingFolder { get; set; } = string.Empty;
    public string CustomInstructions { get; set; } = DEFAULT_INSTRUCTIONS;
    public int TypingDelay { get; set; } = 10;
    public string SelectedGeminiModel { get; set; } = "gemini-2.0-flash-thinking-exp-01-21";
    public bool UseCodebaseIndexing { get; set; } = true;
    public bool IsEnglish { get; set; } = false;  // false = Spanish, true = English

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    public void Save()
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
        }
        catch
        {
            // Silently fail - user will need to reconfigure next time
        }
    }
} 