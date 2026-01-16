using System.Collections.ObjectModel;
using Omoi.Models;

namespace Omoi.Services;

public class ThoughtProcessLogger
{
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public void LogModeDetectionStart()
    {
        Entries.Add(new LogEntry
        {
            Type = LogEntryType.ModeDetectionStart,
            Content = "--- Starting mode detection ---"
        });
    }

    public void LogModePrompt(string prompt)
    {
        Entries.Add(new LogEntry
        {
            Type = LogEntryType.ModePrompt,
            Content = $"Mode Detection Prompt:\n{prompt}"
        });
    }

    public void LogModeResponse(string response)
    {
        Entries.Add(new LogEntry
        {
            Type = LogEntryType.ModeResponse,
            Content = $"Mode Response: {response}"
        });
    }

    public void LogSystemPrompt(ConversationMode mode, string prompt)
    {
        Entries.Add(new LogEntry
        {
            Type = LogEntryType.SystemPrompt,
            Content = $"System Prompt (Mode: {mode.GetIdentifier()}):\n{prompt}"
        });
    }

    public void LogInfo(string message)
    {
        Entries.Add(new LogEntry
        {
            Type = LogEntryType.Info,
            Content = message
        });
    }

    public void Clear()
    {
        Entries.Clear();
    }
}