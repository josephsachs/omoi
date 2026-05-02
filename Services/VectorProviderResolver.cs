namespace Omoi.Services;

public class VectorProviderResolver
{
    private readonly IVectorProvider _provider;

    public VectorProviderResolver(IVectorProvider provider)
    {
        _provider = provider;
    }

    public IVectorProvider GetProviderForModel(string modelId) => _provider;
}
