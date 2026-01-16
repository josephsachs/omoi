using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services;

public class ConversationService
{
    private readonly string _conversationsDir;
    private readonly string _autoSavePath;

    public ConversationService()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _conversationsDir = Path.Combine(home, ".omoi", "conversations");
        _autoSavePath = Path.Combine(_conversationsDir, "autosave.json");
        
        Directory.CreateDirectory(_conversationsDir);
    }

    public async Task SaveConversationAsync(
        List<Message> messages, 
        string currentModeIdentifier, 
        string filepath)
    {
        var data = new ConversationData
        {
            Messages = messages,
            SavedAt = DateTime.Now,
            CurrentModeIdentifier = currentModeIdentifier
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        await File.WriteAllTextAsync(filepath, json);
    }

    public async Task<ConversationData?> LoadConversationAsync(string filepath)
    {
        if (!File.Exists(filepath))
            return null;

        var json = await File.ReadAllTextAsync(filepath);
        return JsonSerializer.Deserialize<ConversationData>(json);
    }

    public async Task AutoSaveAsync(List<Message> messages, string currentModeIdentifier)
    {
        await SaveConversationAsync(messages, currentModeIdentifier, _autoSavePath);
    }

    public async Task<ConversationData?> LoadAutoSaveAsync()
    {
        return await LoadConversationAsync(_autoSavePath);
    }

    public string GetConversationsDirectory()
    {
        return _conversationsDir;
    }

    public string GenerateTimestampedFilename()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return Path.Combine(_conversationsDir, $"conversation_{timestamp}.json");
    }
}