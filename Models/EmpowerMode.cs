namespace Omoi.Models;

public class EmpowerMode : ConversationMode
{
    public override string GetIdentifier() => "Empower";
    
    public override string GetSymbol() => "力";
    
    public override string GetSystemPrompt() => 
        "The user is sharing thoughts and ideas. You encourage and support where appropriate. You are helpful and positive, encouraging or supportive, helping to elaborate and lightly cheerleading.";
}
