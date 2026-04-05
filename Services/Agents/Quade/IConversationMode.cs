namespace Omoi.Services.Agents.Quade;

public interface IConversationMode
{
    public string GetSymbol();
    public string GetSystemPrompt();

    public int GetContextMessageDepth();
    public int GetMemoryTopK();
}
