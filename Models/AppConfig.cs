namespace Omoi.Models;

public enum VectorStorageProvider
{
    Supabase,
    Qdrant
}

public class AppConfig
{
    public string ConversationalModel { get; set; } = "anthropic/claude-3-7-sonnet";
    public string ThoughtModel { get; set; } = "openai/gpt-4.1-nano";
    public string MemoryModel { get; set; } = "anthropic/claude-sonnet-4-5";
    public string VectorModel { get; set; } = string.Empty;
    public string SavedConversationsPath { get; set; } = "~/.Omoi/conversations/";
    public VectorStorageProvider SelectedVectorStorage { get; set; } = VectorStorageProvider.Qdrant;
    public string SupabaseUrl { get; set; } = string.Empty;
    public string QdrantUrl { get; set; } = string.Empty;
    public string Theme { get; set; } = "dark";
    
    public string PersonalityPrompt { get; set; } = "You are Omoi (思), a social chatbot. You use precise descriptions and intellectual terminology; you do not use metaphor, and you avoid flowery speech. You do not use formatting.";
    public int MaxContextMessages { get; set; } = 42;
    public int MemoryStoreInterval { get; set; } = 4;
    public int TopKMemories { get; set; } = 5;
    public float SimilarityThreshold { get; set; } = 0.12f;
    
    public double MainWindowX { get; set; }
    public double MainWindowY { get; set; }
    public double MainWindowWidth { get; set; } = 1000;
    public double MainWindowHeight { get; set; } = 750;
    
    public double ThoughtWindowX { get; set; }
    public double ThoughtWindowY { get; set; }
    public double ThoughtWindowWidth { get; set; } = 700;
    public double ThoughtWindowHeight { get; set; } = 500;
    public bool ThoughtWindowWasOpen { get; set; }
}