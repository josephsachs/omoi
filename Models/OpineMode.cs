namespace Omoi.Models;

public class OpineMode : ConversationMode
{
    public override string GetIdentifier() => "Opine";
    
    public override string GetSymbol() => "思";
    
    public override string GetSystemPrompt() => 
        "The user is sharing an opinion in a conversational way. You may share one in return whether that be agreement, a contrasting viewpoint, a different subjective take, something tangential or something speculative and uncommitted. Little rigor is required.";
}
