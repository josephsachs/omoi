/**
*
*   Hardcoded class for OpenAI model metadata since their /models endpoint does not 
*   return any regarding URIs, formats and capabilities and these vary between models.
*
*   This is less bad than any of the alternatives even though it is horrible. 
*   Fix your shit OpenAI.
*
**/

using System.Collections.Generic;

namespace Omoi.Models;

public enum OpenAiEndpoint
{
    ChatCompletions,
    Embeddings
}

public class OpenAiModelCapabilities
{
    public OpenAiEndpoint Endpoint { get; set; }
    public List<string> Categories { get; set; } = new();
}

public static class OpenAiModelRegistry
{
    private static readonly Dictionary<string, OpenAiModelCapabilities> _registry = new()
    {
        ["gpt-4.1"] = new()
        {
            Endpoint = OpenAiEndpoint.ChatCompletions,
            Categories = new() { "chat", "thought", "memory" }
        },
        ["gpt-4.1-mini"] = new()
        {
            Endpoint = OpenAiEndpoint.ChatCompletions,
            Categories = new() { "chat", "thought", "memory" }
        },
        ["gpt-4.1-nano"] = new()
        {
            Endpoint = OpenAiEndpoint.ChatCompletions,
            Categories = new() { "chat", "thought", "memory" }
        },
        ["gpt-4o"] = new()
        {
            Endpoint = OpenAiEndpoint.ChatCompletions,
            Categories = new() { "chat", "thought", "memory" }
        },
        ["gpt-4o-mini"] = new()
        {
            Endpoint = OpenAiEndpoint.ChatCompletions,
            Categories = new() { "chat", "thought", "memory" }
        },
        ["gpt-3.5-turbo"] = new()
        {
            Endpoint = OpenAiEndpoint.ChatCompletions,
            Categories = new() { "chat", "thought", "memory" }
        },
        ["text-embedding-3-large"] = new()
        {
            Endpoint = OpenAiEndpoint.Embeddings,
            Categories = new() { "vector" }
        },
        ["text-embedding-3-small"] = new()
        {
            Endpoint = OpenAiEndpoint.Embeddings,
            Categories = new() { "vector" }
        },
        ["text-embedding-ada-002"] = new()
        {
            Endpoint = OpenAiEndpoint.Embeddings,
            Categories = new() { "vector" }
        }
    };

    public static OpenAiModelCapabilities? GetCapabilities(string modelId)
    {
        return _registry.TryGetValue(modelId, out var capabilities) ? capabilities : null;
    }

    public static bool IsSupported(string modelId)
    {
        return _registry.ContainsKey(modelId);
    }

    public static IEnumerable<KeyValuePair<string, OpenAiModelCapabilities>> GetAllModels()
    {
        return _registry;
    }

    public static int GetEmbeddingDimensions(string modelId)
    {
        var normalized = modelId.Contains('/') ? modelId.Split('/')[^1] : modelId;
        return normalized switch
        {
            "text-embedding-3-large" => 3072,
            "text-embedding-3-small" => 1536,
            "text-embedding-ada-002" => 1536,
            _ => 1536
        };
    }
}
