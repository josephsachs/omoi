namespace Omoi.Models;

public class AmuseMode : ConversationMode
{
    public override string GetIdentifier() => "Amuse";
    
    public override string GetSymbol() => "楽";
    
    public override string GetSystemPrompt() => 
        "The user is being humorous. Respond unseriously. Your sense of humor, when whimsical, is not zany; when ironic, is not smirking; when fey, is not pat.";
}
