namespace Omoi.Services.Agents.Quade;

public abstract class ConversationMode
{
    public abstract string GetIdentifier();
    public abstract string GetSymbol();
    public abstract string GetSystemPrompt();
}
