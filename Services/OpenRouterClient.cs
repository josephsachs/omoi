using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services;

public class OpenRouterClient : IModelProvider, IVectorProvider
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "https://openrouter.ai/api/v1";
    private const string EMBEDDING_MODEL = "openai/text-embedding-3-large";

    public OpenRouterClient()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BASE_URL);
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://omoi.app");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "Omoi");
    }

    public void SetApiKey(string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        var response = await _httpClient.GetAsync($"{BASE_URL}/models");
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ModelsResponse>(responseJson);

        return result?.Data
            .Select(m => new ModelInfo
            {
                Id = m.Id,
                DisplayName = m.Name,
                Type = "model",
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(m.Created).UtcDateTime,
                Categories = IsEmbeddingModel(m.Id)
                    ? new List<string> { "vector" }
                    : new List<string> { "chat", "thought", "memory" }
            })
            .ToList() ?? new List<ModelInfo>();
    }

    public async Task<string> SendMessageAsync(
        ModelRequestConfig config,
        List<Message> messages,
        string? systemPrompt = null)
    {
        var apiMessages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            apiMessages.Add(new { role = "system", content = systemPrompt });
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

        var response = await _httpClient.PostAsync($"{BASE_URL}/chat/completions", content);

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

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var request = new
        {
            input = text,
            model = EMBEDDING_MODEL,
            dimensions = 3072
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BASE_URL}/embeddings", content);

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

    private static bool IsEmbeddingModel(string modelId) =>
        modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase);

    private class ModelsResponse
    {
        [JsonPropertyName("data")]
        public List<OpenRouterModel> Data { get; set; } = new();
    }

    private class OpenRouterModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public long Created { get; set; }
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
