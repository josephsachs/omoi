using System;
using System.Collections.Generic;

namespace Omoi.Services.Agents.Quade;

public static class ModeRegistry
{
    private static readonly Dictionary<string, Func<ConversationMode>> _modes = new()
    {
        ["Talk"] = () => new TalkMode(),
        ["Empower"] = () => new EmpowerMode(),
        ["Investigate"] = () => new InvestigateMode(),
        ["Opine"] = () => new OpineMode(),
        ["Amuse"] = () => new AmuseMode()
    };

    public static ConversationMode GetMode(string identifier)
    {
        if (_modes.TryGetValue(identifier, out var factory))
        {
            return factory();
        }

        return GetDefaultMode();
    }

    public static ConversationMode GetDefaultMode()
    {
        return new TalkMode();
    }

    public static IEnumerable<string> GetAllModeIdentifiers()
    {
        return _modes.Keys;
    }
}
