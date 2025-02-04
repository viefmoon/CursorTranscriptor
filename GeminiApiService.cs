using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.IO;

namespace CursorTranscriptor;

public class GeminiApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _workingFolder;
    private const string BASE_URL = "https://generativelanguage.googleapis.com";
    private string _selectedModel = "gemini-2.0-flash-thinking-exp-01-21";
    private const int MAX_RETRIES = 3;
    private const int RETRY_DELAY_MS = 1000;

    public GeminiApiService(string apiKey, string workingFolder)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("La API key de Google no puede estar vacía", nameof(apiKey));
        
        _apiKey = apiKey;
        _workingFolder = workingFolder;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetModel(string modelId) => _selectedModel = modelId;

    public static List<GeminiModel> AvailableModels => new()
    {
        new GeminiModel 
        { 
            Id = "gemini-2.0-flash-thinking-exp-01-21",
            Name = "Gemini 2.0 Flash Thinking",
            Description = string.Empty
        },
        new GeminiModel 
        { 
            Id = "gemini-2.0-flash-exp",
            Name = "Gemini 2.0 Flash",
            Description = string.Empty
        },
        new GeminiModel 
        { 
            Id = "gemini-exp-1206",
            Name = "Gemini Experimental 1206",
            Description = string.Empty
        }
    };

    public async Task<string> UploadAudioFileAsync(byte[] audioData)
    {
        var uploadUrl = $"{BASE_URL}/upload/v1beta/files?key={_apiKey}";
        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        
        request.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        request.Headers.Add("X-Goog-Upload-Command", "start");
        request.Headers.Add("X-Goog-Upload-Header-Content-Length", audioData.Length.ToString());
        request.Headers.Add("X-Goog-Upload-Header-Content-Type", "audio/wav");

        var metadata = new { file = new { display_name = "audio_recording" } };
        var jsonMetadata = JsonSerializer.Serialize(metadata).Replace("\"", "'");
        request.Content = new StringContent(jsonMetadata, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error en la subida inicial: {response.StatusCode}");

        var uploadUri = response.Headers.GetValues("X-Goog-Upload-URL").FirstOrDefault()
            ?? throw new Exception("No se recibió URL de subida");

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUri);
        var byteContent = new ByteArrayContent(audioData);
        
        byteContent.Headers.ContentLength = audioData.Length;
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        uploadRequest.Content = byteContent;

        var uploadResponse = await _httpClient.SendAsync(uploadRequest);
        var uploadResponseContent = await uploadResponse.Content.ReadAsStringAsync();

        if (!uploadResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Error en la subida final: {uploadResponse.StatusCode}");

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        
        var fileInfo = JsonSerializer.Deserialize<FileUploadResponse>(uploadResponseContent, options);
        return fileInfo?.File?.Uri ?? throw new Exception("Error al obtener URI del archivo");
    }

    public async Task<string> TranscribeAudioAsync(string fileUri)
    {
        int currentRetry = 0;
        Exception? lastException = null;

        while (currentRetry < MAX_RETRIES)
        {
            try
            {
                var url = $"{BASE_URL}/v1beta/models/{_selectedModel}:generateContent?key={_apiKey}";
                var settings = AppSettings.Load();
                
                string codebaseContext = "";
                if (settings.UseCodebaseIndexing)
                {
                    var codebasePath = Path.Combine(_workingFolder, "codefetch", "codebase.md");
                    if (File.Exists(codebasePath))
                        codebaseContext = await File.ReadAllTextAsync(codebasePath);
                }

                string fullPrompt = settings.UseCodebaseIndexing
                    ? $"{settings.CustomInstructions}\n\nContexto del código base para referencia de archivos:\n{codebaseContext}"
                    : settings.CustomInstructions;

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = fullPrompt },
                                new { file_data = new { mime_type = "audio/wav", file_uri = fileUri } }
                            }
                        }
                    }
                };

                var jsonOptions = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody, jsonOptions),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    if (responseContent.Contains("UNAVAILABLE") || responseContent.Contains("overloaded"))
                    {
                        if (++currentRetry < MAX_RETRIES)
                        {
                            TryNextModel();
                            await Task.Delay(RETRY_DELAY_MS);
                            continue;
                        }
                    }
                    throw new HttpRequestException($"Error en la transcripción: {response.StatusCode}");
                }

                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(
                    responseContent, 
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );
                
                var transcription = geminiResponse?.Candidates?
                    .FirstOrDefault()?.Content?.Parts?
                    .FirstOrDefault()?.Text?.Trim();
                
                return string.IsNullOrWhiteSpace(transcription) 
                    ? "No se pudo obtener la transcripción" 
                    : transcription;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (++currentRetry < MAX_RETRIES)
                {
                    TryNextModel();
                    await Task.Delay(RETRY_DELAY_MS);
                }
                else throw new Exception(
                    "No se pudo completar la transcripción después de varios intentos. " +
                    "Por favor, intente nuevamente en unos momentos.", 
                    lastException
                );
            }
        }

        throw new Exception(
            "No se pudo completar la transcripción después de varios intentos. " +
            "Por favor, intente nuevamente en unos momentos.", 
            lastException
        );
    }

    private void TryNextModel()
    {
        var currentModelIndex = AvailableModels.FindIndex(m => m.Id == _selectedModel);
        var nextModelIndex = (currentModelIndex + 1) % AvailableModels.Count;
        _selectedModel = AvailableModels[nextModelIndex].Id;
    }
}

// Clases de modelo simplificadas
public record FileUploadResponse(FileInfo? File);
public record FileInfo(string? Uri);
public record GeminiResponse(List<Candidate>? Candidates);
public record Candidate(Content? Content);
public record Content(Part[]? Parts);
public record Part(string? Text);
public record GeminiModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
} 