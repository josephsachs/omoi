namespace Omoi.Services.Agents.Quade;

public class TalkMode : ConversationMode
{
    public override string GetIdentifier() => "Talk";

    public override string GetSymbol() => "話";

    public override string GetSystemPrompt() => "";
}
