using System.Collections.Generic;
using System.Threading.Tasks;

namespace Omoi.Services;

public interface IVectorStorage
{
    void SetApiKey(string apiKey, string url);
    Task EnsureReadyAsync(int dimensions);
    Task StoreMemoryAsync(string content, float[] embedding);
    Task<List<Memory>> SearchSimilarMemoriesAsync(float[] queryEmbedding, int topK = 5, float threshold = 0.7f);
}

public class Memory
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public System.DateTime CreatedAt { get; set; }
    public float Similarity { get; set; }
}