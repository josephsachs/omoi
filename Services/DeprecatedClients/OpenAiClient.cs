using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Omoi.Models;
using Omoi.Services;

namespace Omoi.Services.DeprecatedClients;

public class OpenAiClient : IModelProvider, IVectorProvider
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "https://api.openai.com/v1";

    public OpenAiClient()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BASE_URL);
    }

    public void SetApiKey(string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        var models = new List<ModelInfo>();

        foreach (var (modelId, capabilities) in OpenAiModelRegistry.GetAllModels())
        {
            models.Add(new ModelInfo
            {
                Id = modelId,
                DisplayName = FormatDisplayName(modelId),
                Type = "model",
                CreatedAt = DateTime.UtcNow,
                Categories = new List<string>(capabilities.Categories)
            });
        }

        return await Task.FromResult(models);
    }

    public async Task<string> SendMessageAsync(
        ModelRequestConfig config,
        List<Message> messages,
        string? systemPrompt = null)
    {
        var capabilities = OpenAiModelRegistry.GetCapabilities(config.Model);

        if (capabilities == null)
        {
            throw new InvalidOperationException($"Model {config.Model} is not supported");
        }

        if (capabilities.Endpoint != OpenAiEndpoint.ChatCompletions)
        {
            throw new InvalidOperationException($"Model {config.Model} does not support chat completions endpoint");
        }

        var apiMessages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            apiMessages.Add(new
            {
                role = "system",
                content = systemPrompt
            });
        }

        apiMessages.AddRange(messages.Select(m => new
        {
            role = m.IsUser ? "user" : "assistant",
            content = m.Content
        }));

        var request = new
        {
            model = config.Model,
            max_tokens = config.MaxTokens,
            messages = apiMessages.ToArray()
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorJson = JsonSerializer.Deserialize<JsonDocument>(errorContent);
            var errorMessage = errorJson?.RootElement.GetProperty("error").GetProperty("message").GetString()
                ?? "Unknown API error";
            throw new HttpRequestException(errorMessage);
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);

        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string model)
    {
        var request = new
        {
            input = text,
            model = model
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/embeddings", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorJson = JsonSerializer.Deserialize<JsonDocument>(errorContent);
            var errorMessage = errorJson?.RootElement.GetProperty("error").GetProperty("message").GetString()
                ?? "Unknown API error";
            throw new HttpRequestException(errorMessage);
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson);

        return result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();
    }

    private string FormatDisplayName(string modelId)
    {
        return modelId switch
        {
            "gpt-4.1" => "GPT-4.1",
            "gpt-4.1-mini" => "GPT-4.1 Mini",
            "gpt-4.1-nano" => "GPT-4.1 Nano",
            "gpt-4o" => "GPT-4o",
            "gpt-4o-mini" => "GPT-4o Mini",
            "gpt-3.5-turbo" => "GPT-3.5 Turbo",
            "text-embedding-3-large" => "Text Embedding 3 Large",
            "text-embedding-3-small" => "Text Embedding 3 Small",
            "text-embedding-ada-002" => "Text Embedding Ada 002",
            _ => modelId
        };
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; } = new();
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public MessageContent Message { get; set; } = new();
    }

    private class MessageContent
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = new();
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
