using System;
using System.IO;
using System.Text.Json;
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
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to decrypt API key: {ex.Message}");
                            _settings.GeminiApiKey = string.Empty;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error and use defaults
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            // Encrypt API Key before saving
            if (!string.IsNullOrEmpty(_settings.GeminiApiKey))
            {
                _settings.EncryptedGeminiApiKey = Protect(_settings.GeminiApiKey);
            }
            else
            {
                _settings.EncryptedGeminiApiKey = string.Empty;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsPath, json);

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
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
