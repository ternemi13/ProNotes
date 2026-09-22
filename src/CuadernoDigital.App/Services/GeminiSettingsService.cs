using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CuadernoDigital.App.Services;

public sealed class GeminiSettingsService
{
    private readonly string settingsPath;

    public GeminiSettingsService(string? settingsDirectory = null)
    {
        settingsDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProNotes");
        Directory.CreateDirectory(settingsDirectory);
        settingsPath = Path.Combine(settingsDirectory, "gemini.key");
    }

    public bool HasApiKey => File.Exists(settingsPath);

    public string? LoadApiKey()
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(settingsPath);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public void SaveApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ClearApiKey();
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(apiKey.Trim());
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(settingsPath, protectedBytes);
    }

    public void ClearApiKey()
    {
        if (File.Exists(settingsPath))
        {
            File.Delete(settingsPath);
        }
    }
}
