using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services;

/// <summary>
/// Service for managing application settings with JSON file persistence.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings;

    public AppSettings Settings => _settings;

    public event EventHandler? SettingsChanged;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "QuickTranslate");

        // Ensure the directory exists
        Directory.CreateDirectory(appFolder);

        _settingsPath = Path.Combine(appFolder, "settings.json");
        _settings = new AppSettings();

        // Load settings on initialization
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                ProcessLoadedJson(json);
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"IO error loading settings: {ex.Message}");
            _settings = new AppSettings();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"JSON error loading settings: {ex.Message}");
            _settings = new AppSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unexpected error loading settings: {ex.Message}");
            _settings = new AppSettings();
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                ProcessLoadedJson(json);
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"IO error loading settings async: {ex.Message}");
            _settings = new AppSettings();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"JSON error loading settings async: {ex.Message}");
            _settings = new AppSettings();
        }
        catch (Exception ex) {
             System.Diagnostics.Debug.WriteLine($"Unexpected error loading settings async: {ex.Message}");
             _settings = new AppSettings();
        }
    }

    private void ProcessLoadedJson(string json)
    {
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);
        if (loaded != null)
        {
            _settings = loaded;

            // Decrypt API Key
            if (!string.IsNullOrEmpty(_settings.EncryptedGeminiApiKey))
            {
                try
                {
                    _settings.GeminiApiKey = Unprotect(_settings.EncryptedGeminiApiKey);
                }
                catch (System.Security.Cryptography.CryptographicException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cryptography error decrypting API key: {ex.Message}");
                    _settings.GeminiApiKey = string.Empty;
                }
            }
        }
    }

    public void Save()
    {
        try
        {
            PrepareSettingsForSave();

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsPath, json);

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"IO error saving settings: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unexpected error saving settings: {ex.Message}");
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            PrepareSettingsForSave();

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            await File.WriteAllTextAsync(_settingsPath, json);

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"IO error saving settings async: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unexpected error saving settings async: {ex.Message}");
        }
    }

    private void PrepareSettingsForSave()
    {
        // Encrypt API Key before saving
        if (!string.IsNullOrEmpty(_settings.GeminiApiKey))
        {
            try
            {
                _settings.EncryptedGeminiApiKey = Protect(_settings.GeminiApiKey);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cryptography error encrypting API key: {ex.Message}");
                _settings.EncryptedGeminiApiKey = string.Empty;
            }
        }
        else
        {
            _settings.EncryptedGeminiApiKey = string.Empty;
        }
    }

    private static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var encrypted = System.Security.Cryptography.ProtectedData.Protect(
            bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string Unprotect(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
        var bytes = Convert.FromBase64String(encryptedText);
        var decrypted = System.Security.Cryptography.ProtectedData.Unprotect(
            bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }
}
