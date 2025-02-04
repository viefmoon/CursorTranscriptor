using System.Windows;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using MessageBox = System.Windows.MessageBox;
using System.Linq;
using CursorTranscriptor.Resources;

namespace CursorTranscriptor;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _hasChanges;
    private Process? _currentProcess;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        
        // Inicializar controles existentes
        txtApiKey.Text = settings.GoogleApiKey;
        txtWorkingFolder.Text = settings.WorkingFolder;
        txtCustomInstructions.Text = settings.CustomInstructions;
        
        // Inicializar selector de modelos
        var models = GeminiApiService.AvailableModels;
        ModelSelector.ItemsSource = models;
        
        // Seleccionar el modelo actual
        var selectedModel = models.FirstOrDefault(m => m.Id == settings.SelectedGeminiModel);
        if (selectedModel != null)
        {
            ModelSelector.SelectedValue = selectedModel.Id;
        }
        else
        {
            ModelSelector.SelectedIndex = 0; // Seleccionar el primer modelo por defecto
        }
        
        // Inicializar checkbox de codebase
        chkUseCodebase.IsChecked = settings.UseCodebaseIndexing;
        
        InitializeLanguage();
    }

    private void UpdateUILanguage()
    {
        // Actualizar todos los textos de la UI
        Title = Strings.Settings;
        btnSave.Content = Strings.SaveChanges;
        btnCancel.Content = Strings.Cancel;
        btnGenerateCodebase.Content = Strings.GenerateCodebase;
        chkUseCodebase.Content = Strings.UseCodebaseIndexing;
        btnRestore.Content = Strings.RestoreDefaults;
    }

    private void InitializeLanguage()
    {
        // Inicializar selector de idioma
        LanguageSelector.SelectedIndex = _settings.IsEnglish ? 1 : 0;
        
        // Monitorear cambios
        LanguageSelector.SelectionChanged += (s, e) => 
        {
            _hasChanges = true;
            Strings.IsEnglish = LanguageSelector.SelectedIndex == 1;
            UpdateUILanguage();
        };

        // Actualizar UI inicial
        UpdateUILanguage();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void btnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta de trabajo",
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrEmpty(txtWorkingFolder.Text))
        {
            dialog.InitialDirectory = txtWorkingFolder.Text;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            txtWorkingFolder.Text = dialog.SelectedPath;
            _hasChanges = true;
        }
    }

    private async void btnGenerateCodebase_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnGenerateCodebase.IsEnabled = false;
            txtStatus.Text = "Generando codebase...";

            var workingDirectory = txtWorkingFolder.Text;
            if (!Directory.Exists(workingDirectory))
            {
                MessageBox.Show("La carpeta de trabajo no existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Configurar el proceso
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c npx codefetch",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            // Iniciar el proceso
            _currentProcess = new Process { StartInfo = startInfo };
            
            _currentProcess.OutputDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Dispatcher.Invoke(() => txtStatus.Text += args.Data + "\n");
                }
            };

            _currentProcess.ErrorDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Dispatcher.Invoke(() => txtStatus.Text += "Error: " + args.Data + "\n");
                }
            };

            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();

            await _currentProcess.WaitForExitAsync();

            var codebasePath = Path.Combine(workingDirectory, "codefetch", "codebase.md");
            if (File.Exists(codebasePath))
            {
                txtStatus.Text += "\nArchivo generado correctamente en:\n" + codebasePath;
            }
            else
            {
                txtStatus.Text += "\nError: No se encontró el archivo generado.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al generar codebase: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnGenerateCodebase.IsEnabled = true;
        }
    }

    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtApiKey.Text))
        {
            MessageBox.Show(
                "Por favor ingresa una API Key válida", 
                "Error de validación", 
                MessageBoxButton.OK, 
                MessageBoxImage.Warning
            );
            txtApiKey.Focus();
            return;
        }

        try
        {
            // Guardar todos los cambios
            _settings.GoogleApiKey = txtApiKey.Text.Trim();
            _settings.WorkingFolder = txtWorkingFolder.Text.Trim();
            _settings.CustomInstructions = txtCustomInstructions.Text.Trim();
            _settings.UseCodebaseIndexing = chkUseCodebase.IsChecked ?? true;
            
            if (ModelSelector.SelectedItem is GeminiModel selectedModel)
            {
                _settings.SelectedGeminiModel = selectedModel.Id;
            }
            
            _settings.IsEnglish = LanguageSelector.SelectedIndex == 1;
            
            // Guardar en archivo
            _settings.Save();
            
            // Cerrar ventana con éxito
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al guardar la configuración: {ex.Message}", 
                "Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error
            );
        }
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();  // Simplemente cerramos, OnClosing se encargará de verificar los cambios
    }

    private void btnRestore_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            Strings.RestoreDefaultsConfirm,
            Strings.RestoreDefaultsTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            txtCustomInstructions.Text = AppSettings.DEFAULT_INSTRUCTIONS;
            _hasChanges = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Primero verificamos si hay un proceso en ejecución
        if (_currentProcess != null && !_currentProcess.HasExited)
        {
            var result = MessageBox.Show(
                "Hay un proceso en ejecución. ¿Desea cancelarlo?",
                "Proceso en ejecución",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _currentProcess.Kill();
                }
                catch { }
            }
            else
            {
                e.Cancel = true;
                return;
            }
        }

        // Luego verificamos si hay cambios sin guardar, pero solo si no estamos guardando
        if (_hasChanges && DialogResult != true)
        {
            var result = MessageBox.Show(
                "Hay cambios sin guardar. ¿Desea descartarlos?",
                "Cambios pendientes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }
} 