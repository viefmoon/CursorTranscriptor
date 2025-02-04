using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;  // Para ModifierKeys y Key
using NAudio.Wave;
using Newtonsoft.Json;
using WindowsInput;
using WindowsInput.Native;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;  // Para Icon
using Application = System.Windows.Application;  // Para evitar ambigüedad con System.Windows.Forms.Application
using MessageBox = System.Windows.MessageBox;    // Para evitar ambigüedad con System.Windows.Forms.MessageBox
using Clipboard = System.Windows.Clipboard;  // Resolver ambigüedad de Clipboard
using System.Windows.Media;
using System.ComponentModel;  // Agregado para CancelEventArgs
using CursorTranscriptor.Resources;

namespace CursorTranscriptor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _memoryStream;
    private WaveFileWriter? _writer;
    private readonly AppSettings _settings;
    private readonly KeyboardHook _keyboardHook = new();
    private bool _isRecording;
    private NotifyIcon? _trayIcon;
    private System.Windows.Forms.Timer? _blinkTimer;
    private bool _isRedIcon;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        
        // Inicializar idioma
        Strings.IsEnglish = _settings.IsEnglish;
        Strings.CurrentLanguageChanged += (s, e) => UpdateUILanguage();
        
        InitializeHotKeys();
        InitializeTrayIcon();
        InitializeBlinkTimer();
        
        MouseLeftButtonDown += (s, e) => DragMove();
        PositionWindow();
    }

    private void InitializeHotKeys()
    {
        try
        {
            _keyboardHook.KeyCombinationPressed += KeyboardHook_KeyCombinationPressed;
            _keyboardHook.RegisterHotKey(ModifierKeys.None, Key.F9);  // Solo registramos F9
            txtStatus.Text = Strings.Ready;
            
            // Para debug
            ShowNotification("Hotkeys", "F9 registrada correctamente");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al registrar teclas rápidas: {ex.Message}", 
                "Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error
            );
        }
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetExecutingAssembly().Location),
                Visible = true,
                Text = "Smart Transcription"
            };
        }
        catch
        {
            // Si no se puede cargar el ícono, continuamos sin él
            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "Smart Transcription"
            };
        }
    }

    private void InitializeBlinkTimer()
    {
        _blinkTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _blinkTimer.Tick += (s, e) =>
        {
            _isRedIcon = !_isRedIcon;
            recordingIndicator.Fill = new SolidColorBrush(
                _isRedIcon ? Colors.Red : Colors.DarkRed);
        };
    }

    private void PositionWindow()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = screenWidth - Width - 20;
        Top = screenHeight - Height - 40;
    }

    private async void KeyboardHook_KeyCombinationPressed(ModifierKeys modifier, Key key)
    {
        try
        {
            // Para debug
            ShowNotification("Tecla presionada", $"Tecla: {key}, Modificador: {modifier}");

            if (key == Key.F9)  // Solo verificamos F9
            {
                if (!_isRecording)
                {
                    await StartRecording();
                    ShowNotification("Grabación", "Iniciando grabación...");
                }
                else
                {
                    await StopRecording();
                    ShowNotification("Grabación", "Grabación detenida");
                }
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Error", $"Error: {ex.Message}");
        }
    }

    private bool IsTextFieldFocused()
    {
        // Temporalmente retornamos true para probar la grabación
        return true;

        /*var focusedHandle = NativeMethods.GetForegroundWindow();
        if (focusedHandle == IntPtr.Zero) return false;

        var focusedElement = NativeMethods.GetFocusedElement(focusedHandle);
        return NativeMethods.IsTextBox(focusedElement);*/
    }

    private async Task StartRecording()
    {
        try
        {
            _waveIn?.Dispose();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 1),
                BufferMilliseconds = 50
            };

            _memoryStream = new MemoryStream();
            _writer = new WaveFileWriter(new NonClosingStreamWrapper(_memoryStream), _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, e) => 
                _writer?.Write(e.Buffer, 0, e.BytesRecorded);

            _waveIn.StartRecording();
            _isRecording = true;
            
            recordingIndicator.Fill = new SolidColorBrush(Colors.Red);
            _blinkTimer?.Start();
            txtStatus.Text = "Recording...";
        }
        catch (Exception ex)
        {
            throw new Exception("Error al iniciar grabación", ex);
        }
    }

    private async Task StopRecording()
    {
        if (!_isRecording) return;

        try
        {
            _waveIn?.StopRecording();
            _writer?.Dispose();
            _blinkTimer?.Stop();
            recordingIndicator.Fill = new SolidColorBrush(Colors.Gray);
            
            if (_memoryStream?.Length > 0)
            {
                txtStatus.Text = "Transcribiendo...";
                var audioData = _memoryStream.ToArray();
                var transcription = await GetTranscriptionFromGeminiAsync(audioData);
                await SimulateTyping(transcription);
            }

            _isRecording = false;
            txtStatus.Text = "Ready (F9 to start, F10 to stop)";
        }
        catch (Exception ex)
        {
            throw new Exception("Error al detener grabación", ex);
        }
        finally
        {
            _memoryStream?.Dispose();
            _memoryStream = null;
            _writer = null;
        }
    }

    private async Task<string> GetTranscriptionFromGeminiAsync(byte[] audioData)
    {
        var geminiService = new GeminiApiService(_settings.GoogleApiKey, _settings.WorkingFolder);
        var fileUri = await geminiService.UploadAudioFileAsync(audioData);
        return await geminiService.TranscribeAudioAsync(fileUri);
    }

    private async Task SimulateTyping(string text)
    {
        var simulator = new InputSimulator();
        foreach (var word in text.Split(' '))
        {
            if (word.StartsWith("@"))
            {
                foreach (char c in word)
                {
                    simulator.Keyboard.TextEntry(c);
                    await Task.Delay(2);
                }
                simulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                await Task.Delay(5);
            }
            else
            {
                foreach (char c in word)
                {
                    simulator.Keyboard.TextEntry(c);
                    await Task.Delay(1);
                }
            }
            
            simulator.Keyboard.TextEntry(' ');
            await Task.Delay(1);
        }
    }

    private void ShowNotification(string title, string message)
    {
        _trayIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    private void btnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings);
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void btnMinimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void btnClose_Click(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();

    protected override void OnClosing(CancelEventArgs e)
    {
        _trayIcon?.Dispose();
        _keyboardHook.Dispose();
        _blinkTimer?.Dispose();
        _waveIn?.Dispose();
        base.OnClosing(e);
    }

    private void UpdateUILanguage()
    {
        Title = Strings.AppTitle;
        txtStatus.Text = _isRecording ? Strings.Recording : Strings.Ready;
        // ... actualizar demás textos ...
    }
}

/// <summary>
/// Clase auxiliar que envuelve un Stream y evita que se disponga (dispose) el stream subyacente.
/// Esto permite que al cerrar el WaveFileWriter el MemoryStream siga siendo accesible.
/// </summary>
public class NonClosingStreamWrapper : Stream
{
    private readonly Stream _stream;

    public NonClosingStreamWrapper(Stream stream)
    {
        _stream = stream;
    }

    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => _stream.CanWrite;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush()
    {
        _stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        // No se dispone el stream subyacente.
    }
}

// Clase auxiliar para interactuar con Windows
internal static class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetFocus();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static IntPtr GetFocusedElement(IntPtr windowHandle)
    {
        return GetFocus();
    }

    public static bool IsTextBox(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;

        StringBuilder className = new StringBuilder(256);
        GetClassName(handle, className, className.Capacity);
        string classNameStr = className.ToString().ToLower();
        Console.WriteLine($"[NativeMethods] Checking class name: {classNameStr}");

        // Lista expandida de clases que consideramos campos de texto
        return classNameStr.Contains("edit") || 
               classNameStr.Contains("text") || 
               classNameStr.Contains("richedit") ||
               classNameStr.Contains("textbox") ||
               classNameStr.Contains("textarea") ||
               classNameStr.Contains("ttext") ||
               classNameStr.Contains("scintilla") ||  // Para editores de código
               classNameStr.Contains("memo") ||       // Para campos de texto grandes
               classNameStr == "chrome_widgetwin_1" || // Para campos de texto en Chrome
               classNameStr.Contains("internetexplorer_server") || // Para IE/Edge
               classNameStr.Contains("mozillawindowclass"); // Para Firefox
    }
}