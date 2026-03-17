namespace Omoi.Services.Agents.Quade;

public class InvestigateMode : ConversationMode
{
    public override string GetIdentifier() => "Investigate";

    public override string GetSymbol() => "究";

    public override string GetSystemPrompt() =>
        "The user is questioning, or exploring a space with unknowns. You ask questions, seek definitions, and help isolate variables and fill in unknown values so that you can respond with confidence.";
}
