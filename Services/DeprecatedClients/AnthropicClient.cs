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

public class AnthropicClient : IModelProvider
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "https://api.anthropic.com/v1";
    private const string API_VERSION = "2023-06-01";

    public AnthropicClient()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BASE_URL);
    }

    public void SetApiKey(string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", API_VERSION);
    }

    public async Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        var response = await _httpClient.GetAsync("https://api.anthropic.com/v1/models");

        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<ModelsResponse>(content);

        return result?.Data
            .Where(m => m.Type == "model")
            .OrderByDescending(m => m.CreatedAt)
            .ToList() ?? new List<ModelInfo>();
    }

    public async Task<string> SendMessageAsync(
        ModelRequestConfig config,
        List<Message> messages,
        string? systemPrompt = null)
    {
        var request = new
        {
            model = config.Model,
            max_tokens = config.MaxTokens,
            system = systemPrompt ?? string.Empty,
            messages = messages.Select(m => new
            {
                role = m.IsUser ? "user" : "assistant",
                content = m.Content
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var errorJson = JsonSerializer.Deserialize<JsonDocument>(errorContent);
            var errorMessage = errorJson?.RootElement.GetProperty("error").GetProperty("message").GetString()
                ?? "Unknown API error";
            throw new HttpRequestException(errorMessage);
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<MessageResponse>(responseJson);

        return result?.Content?.FirstOrDefault()?.Text ?? string.Empty;
    }

    private class ModelsResponse
    {
        [JsonPropertyName("data")]
        public List<ModelInfo> Data { get; set; } = new();
    }

    private class MessageResponse
    {
        [JsonPropertyName("content")]
        public List<ContentBlock> Content { get; set; } = new();
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
