using Omoi.Models;

namespace Omoi.Services;

public class ModelProviderResolver
{
    private readonly IModelProvider _provider;

    public ModelProviderResolver(IModelProvider provider)
    {
        _provider = provider;
    }

    public IModelProvider GetProviderForModel(string modelId) => _provider;
}
