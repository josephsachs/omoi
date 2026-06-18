using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Omoi.Services;

public class SupabaseClient : IVectorStorage
{
    private readonly HttpClient _httpClient;
    private string _supabaseUrl = string.Empty;
    private const string MEMORIES_TABLE = "memories";
    private bool _tableInitialized = false;

    public SupabaseClient()
    {
        _httpClient = new HttpClient();
    }

    public void SetApiKey(string apiKey, string projectUrl)
    {
        _supabaseUrl = projectUrl.TrimEnd('/');
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("apikey", apiKey);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task EnsureReadyAsync(int dimensions)
    {
        if (_tableInitialized)
            return;

        try
        {
            var testResponse = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/{MEMORIES_TABLE}?limit=1");
            
            if (testResponse.IsSuccessStatusCode)
            {
                _tableInitialized = true;
                return;
            }
        }
        catch
        {
        }

        throw new HttpRequestException(
            $"Table '{MEMORIES_TABLE}' does not exist. Please create it manually in Supabase SQL Editor.\n\n" +
            $"You must use index dimensions compatible with the selected vector embedding model. Mismatched " +
            "settings will cause undefined behavior. See the Supabase and OpenAIs manual for detailed specs."
        );
    }

    public async Task StoreMemoryAsync(string content, float[] embedding)
    {
        var embeddingString = "[" + string.Join(",", embedding.Select(f => f.ToString("G"))) + "]";

        var record = new
        {
            content = content,
            embedding = embeddingString
        };

        var json = JsonSerializer.Serialize(record);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{_supabaseUrl}/rest/v1/{MEMORIES_TABLE}",
            httpContent
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to store memory: {errorContent}");
        }
    }

    public async Task<List<Memory>> SearchSimilarMemoriesAsync(
        float[] queryEmbedding,
        int topK = 5,
        float threshold = 0.7f)
    {
        var embeddingString = "[" + string.Join(",", queryEmbedding.Select(f => f.ToString("G"))) + "]";

        var rpcParams = new
        {
            query_embedding = embeddingString,
            match_threshold = threshold,
            match_count = topK
        };

        var json = JsonSerializer.Serialize(rpcParams);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{_supabaseUrl}/rest/v1/rpc/match_memories",
            content
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to search memories: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<List<SupabaseMemory>>(responseJson);

        return result?.Select(m => new Memory
        {
            Id = m.Id.ToString(),
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            Similarity = m.Similarity
        }).ToList() ?? new List<Memory>();
    }

    private class SupabaseMemory
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("similarity")]
        public float Similarity { get; set; }
    }
}