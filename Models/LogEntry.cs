using System;

namespace Omoi.Models;

public enum LogEntryType
{
    ModeDetectionStart,
    ModePrompt,
    ModeResponse,
    SystemPrompt,
    Info,
    Error
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogEntryType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}