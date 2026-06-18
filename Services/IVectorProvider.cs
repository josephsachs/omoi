using System.Threading.Tasks;

namespace Omoi.Services;

public interface IVectorProvider
{
    Task<float[]> GetEmbeddingAsync(string text, string model);
    void SetApiKey(string apiKey);
}