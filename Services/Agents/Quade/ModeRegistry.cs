using System;
using System.Collections.Generic;

namespace Omoi.Services.Agents.Quade;

public static class ModeRegistry
{
    private static readonly Dictionary<string, Func<ConversationMode>> _modes = new()
    {
        ["Empower"] = () => new EmpowerMode(),
        ["Investigate"] = () => new InvestigateMode(),
        ["Opine"] = () => new OpineMode(),
        ["Critique"] = () => new CritiqueMode(),
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
        return new EmpowerMode();
    }

    public static IEnumerable<string> GetAllModeIdentifiers()
    {
        return _modes.Keys;
    }
}
