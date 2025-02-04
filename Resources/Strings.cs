namespace CursorTranscriptor.Resources;

public static class Strings
{
    private static bool _isEnglish;

    public static bool IsEnglish
    {
        get => _isEnglish;
        set
        {
            _isEnglish = value;
            CurrentLanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? CurrentLanguageChanged;

    public static string AppTitle => IsEnglish ? "Cursor Transcription" : "Cursor Transcription";
    public static string Settings => IsEnglish ? "Settings" : "Configuración";
    public static string GoogleApiKey => IsEnglish ? "Google API Key:" : "Google API Key:";
    public static string GeminiModel => IsEnglish ? "Gemini Model:" : "Modelo de Gemini:";
    public static string WorkingFolder => IsEnglish ? "Working Folder:" : "Carpeta de trabajo:";
    public static string CustomInstructions => IsEnglish ? "Custom Instructions:" : "Instrucciones personalizadas:";
    public static string GenerateCodebase => IsEnglish ? "Generate Project" : "Generar Proyecto";
    public static string UseCodebaseIndexing => IsEnglish ? "Use project indexing" : "Usar indexación del proyecto";
    public static string SaveChanges => IsEnglish ? "Save Changes" : "Guardar cambios";
    public static string Cancel => IsEnglish ? "Cancel" : "Cancelar";
    public static string RestoreDefaults => IsEnglish ? "Restore Defaults" : "Restaurar valores por defecto";
    public static string Ready => IsEnglish 
        ? "Ready (F9 to start/stop)" 
        : "Listo (F9 para iniciar/detener)";
    public static string Recording => IsEnglish ? "Recording..." : "Grabando...";
    public static string Transcribing => IsEnglish ? "Transcribing..." : "Transcribiendo...";
    public static string Language => IsEnglish ? "Language:" : "Idioma:";

    // Mensajes
    public static string UnsavedChanges => IsEnglish 
        ? "You have unsaved changes. Do you want to discard them?"
        : "Hay cambios sin guardar. ¿Desea descartarlos?";
    
    public static string UnsavedChangesTitle => IsEnglish 
        ? "Unsaved Changes"
        : "Cambios sin guardar";

    public static string ProcessRunning => IsEnglish
        ? "There is a process running. Do you want to cancel it?"
        : "Hay un proceso en ejecución. ¿Desea cancelarlo?";

    public static string Error => IsEnglish ? "Error" : "Error";
    public static string Warning => IsEnglish ? "Warning" : "Advertencia";
    public static string Information => IsEnglish ? "Information" : "Información";

    public static string RestoreDefaultsConfirm => IsEnglish 
        ? "Are you sure you want to restore default instructions?"
        : "¿Estás seguro de que quieres restaurar las instrucciones por defecto?";

    public static string RestoreDefaultsTitle => IsEnglish 
        ? "Confirm Restore"
        : "Confirmar restauración";
} 