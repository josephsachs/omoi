using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Omoi.Services;

public class QdrantClient : IVectorStorage
{
    private readonly HttpClient _httpClient;
    private string _qdrantUrl = string.Empty;
    private const string COLLECTION_NAME = "memories";

    public QdrantClient()
    {
        _httpClient = new HttpClient();
    }

    public void SetApiKey(string apiKey, string url)
    {
        _qdrantUrl = url.TrimEnd('/');
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
    }

    public async Task EnsureReadyAsync(int dimensions)
    {
        var checkResponse = await _httpClient.GetAsync($"{_qdrantUrl}/collections/{COLLECTION_NAME}");

        if (checkResponse.IsSuccessStatusCode)
        {
            return;
        }

        var createRequest = new
        {
            vectors = new
            {
                size = dimensions,
                distance = "Cosine"
            }
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var createResponse = await _httpClient.PutAsync(
            $"{_qdrantUrl}/collections/{COLLECTION_NAME}",
            content
        );

        if (!createResponse.IsSuccessStatusCode)
        {
            var errorContent = await createResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create Qdrant collection: {errorContent}");
        }
    }

    public async Task StoreMemoryAsync(string content, float[] embedding)
    {
        var pointId = Guid.NewGuid().ToString();
        var point = new
        {
            points = new[]
            {
                new
                {
                    id = pointId,
                    vector = embedding,
                    payload = new
                    {
                        content = content,
                        created_at = DateTime.UtcNow.ToString("o")
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(point);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(
            $"{_qdrantUrl}/collections/{COLLECTION_NAME}/points",
            httpContent
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to store memory in Qdrant: {errorContent}");
        }
    }

    public async Task<List<Memory>> SearchSimilarMemoriesAsync(
        float[] queryEmbedding,
        int topK = 5,
        float threshold = 0.7f)
    {
        var searchRequest = new
        {
            vector = queryEmbedding,
            limit = topK,
            score_threshold = threshold,
            with_payload = true
        };

        var json = JsonSerializer.Serialize(searchRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{_qdrantUrl}/collections/{COLLECTION_NAME}/points/search",
            content
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to search memories in Qdrant: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<QdrantSearchResponse>(responseJson);

        return result?.Result?.Select(r => new Memory
        {
            Id = r.Id,
            Content = r.Payload.Content,
            CreatedAt = DateTime.Parse(r.Payload.CreatedAt),
            Similarity = r.Score
        }).ToList() ?? new List<Memory>();
    }

    private class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchResult> Result { get; set; } = new();
    }

    private class QdrantSearchResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("payload")]
        public QdrantPayload Payload { get; set; } = new();
    }

    private class QdrantPayload
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}