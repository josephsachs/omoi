using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Omoi.Services;

public class CredentialsService
{
    public const string OPENROUTER = "openrouter";
    public const string ANLATAN = "anlatan";
    public const string SUPABASE = "supabase";
    public const string QDRANT = "qdrant";

    private readonly string _credentialsPath;

    public CredentialsService()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(home, ".omoi");
        _credentialsPath = Path.Combine(configDir, "credentials.json");
        
        Directory.CreateDirectory(configDir);
    }

    public async Task<bool> HasApiKeyAsync(string provider)
    {
        var key = await GetApiKeyAsync(provider);
        return !string.IsNullOrWhiteSpace(key);
    }

    public async Task<string?> GetApiKeyAsync(string provider)
    {
        if (!File.Exists(_credentialsPath))
            return null;

        var credentials = await LoadCredentialsAsync();
        
        if (!credentials.TryGetValue(provider, out var value))
            return null;

        return value;
    }

    public async Task SetApiKeyAsync(string provider, string apiKey)
    {
        var credentials = await LoadCredentialsAsync();
        
        credentials[provider] = apiKey;
        
        await SaveCredentialsAsync(credentials);
    }

    public async Task DeleteApiKeyAsync(string provider)
    {
        var credentials = await LoadCredentialsAsync();
        
        credentials.Remove(provider);
        
        await SaveCredentialsAsync(credentials);
    }

    private async Task<Dictionary<string, string>> LoadCredentialsAsync()
    {
        if (!File.Exists(_credentialsPath))
            return new Dictionary<string, string>();

        var json = await File.ReadAllTextAsync(_credentialsPath);
        
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
            ?? new Dictionary<string, string>();
    }

    private async Task SaveCredentialsAsync(Dictionary<string, string> credentials)
    {
        var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        await File.WriteAllTextAsync(_credentialsPath, json);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || 
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            File.SetUnixFileMode(_credentialsPath, 
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}