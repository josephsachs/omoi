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

public class DeepSeekClient : IModelProvider
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "https://api.deepseek.com";

    public DeepSeekClient()
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

        foreach (var (modelId, capabilities) in DeepSeekModelRegistry.GetAllModels())
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
        var capabilities = DeepSeekModelRegistry.GetCapabilities(config.Model);

        if (capabilities == null)
        {
            throw new InvalidOperationException($"Model {config.Model} is not supported");
        }

        if (capabilities.Endpoint != DeepSeekEndpoint.ChatCompletions)
        {
            throw new InvalidOperationException($"Model {config.Model} does not support chat completions endpoint");
        }

        var apiMessages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            apiMessages.Add(new
            {
                role = "system",
                content = $"{systemPrompt} Be judicious with compliments and flattery. Don't use emotes or stage direction unless responding to user in kind."
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

        var response = await _httpClient.PostAsync("https://api.deepseek.com/chat/completions", content);

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

    private string FormatDisplayName(string modelId)
    {
        return modelId switch
        {
            "deepseek-chat" => "DeepSeek Chat",
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
}
