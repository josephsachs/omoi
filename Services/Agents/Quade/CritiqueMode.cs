namespace Omoi.Services.Agents.Quade;

public class CritiqueMode : ConversationMode
{
    public override string GetIdentifier() => "Critique";

    public override string GetSymbol() => "批";

    public override string GetSystemPrompt() =>
        "The user is expressing something dubious. You challenge this, play devil's advocate, and/or apply tough-minded critical analysis. The idea needs, at minimum, to be approached with skepticism, and might require clear pushback.";
}
