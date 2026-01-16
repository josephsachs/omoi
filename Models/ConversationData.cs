using System;
using System.Collections.Generic;

namespace Omoi.Models;

public class ConversationData
{
    public List<Message> Messages { get; set; } = new();
    public DateTime SavedAt { get; set; }
    public string CurrentModeIdentifier { get; set; } = "Empower";
}