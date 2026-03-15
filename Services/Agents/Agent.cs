using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services.Agents;

public abstract class Agent
{
    protected readonly ModelProviderResolver ProviderResolver;
    protected readonly ConfigService ConfigService;
    protected readonly ThoughtProcessLogger Logger;

    private readonly VectorProviderResolver _vectorProviderResolver;
    private readonly VectorStorageResolver _vectorStorageResolver;

    protected Agent(
        ModelProviderResolver providerResolver,
        VectorProviderResolver vectorProviderResolver,
        VectorStorageResolver vectorStorageResolver,
        ConfigService configService,
        ThoughtProcessLogger logger)
    {
        ProviderResolver = providerResolver;
        _vectorProviderResolver = vectorProviderResolver;
        _vectorStorageResolver = vectorStorageResolver;
        ConfigService = configService;
        Logger = logger;
    }

    public abstract Task<AgentResult> Handle(AgentInput input);

    protected async Task<string> Query(string prompt, IReadOnlyList<Message> context)
    {
        var config = await ConfigService.LoadConfigAsync();
        var provider = ProviderResolver.GetProviderForModel(config.ThoughtModel);
        var requestConfig = new ModelRequestConfig { Model = config.ThoughtModel, MaxTokens = 1 };
        return await provider.SendMessageAsync(requestConfig, new List<Message>(context), prompt);
    }

    protected async Task<string> Generate(ModelType modelType, IReadOnlyList<Message> context, string systemPrompt)
    {
        var config = await ConfigService.LoadConfigAsync();
        var modelId = modelType switch
        {
            ModelType.Query => config.ThoughtModel,
            ModelType.Thought => config.ThoughtModel,
            ModelType.Memory => config.MemoryModel,
            ModelType.Conversational => config.ConversationalModel,
            _ => config.ConversationalModel
        };
        var provider = ProviderResolver.GetProviderForModel(modelId);
        var requestConfig = new ModelRequestConfig { Model = modelId, MaxTokens = 4096 };
        return await provider.SendMessageAsync(requestConfig, new List<Message>(context), systemPrompt);
    }

    protected async Task<IReadOnlyList<Memory>> RetrieveMemories(string query, int topK, float threshold)
    {
        var config = await ConfigService.LoadConfigAsync();

        if (string.IsNullOrEmpty(config.VectorModel))
            return new List<Memory>();

        try
        {
            var vectorProvider = _vectorProviderResolver.GetProviderForModel(config.VectorModel);
            var vectorStorage = _vectorStorageResolver.GetStorage(config.SelectedVectorStorage);
            var embedding = await vectorProvider.GetEmbeddingAsync(query);
            return await vectorStorage.SearchSimilarMemoriesAsync(embedding, topK, threshold);
        }
        catch
        {
            return new List<Memory>();
        }
    }

    protected async Task<bool> StoreMemories(IReadOnlyList<Message> history, string summarizationInstruction)
    {
        var config = await ConfigService.LoadConfigAsync();

        var unmemoized = history.Where(m => !m.IsMemorized).ToList();
        if (unmemoized.Count < config.MemoryStoreInterval)
            return false;

        Logger.LogInfo($"Processing {unmemoized.Count} messages into memory...");

        if (string.IsNullOrEmpty(config.MemoryModel))
            return false;

        var transcript = string.Join("\n\n", unmemoized.Select(m =>
            $"{(m.IsUser ? "User" : "Assistant")}: \"{m.Content}\""));

        var fullPrompt = $"Here is a conversation transcript:\n\n{transcript}\n\n{summarizationInstruction}";

        var provider = ProviderResolver.GetProviderForModel(config.MemoryModel);
        var requestConfig = new ModelRequestConfig { Model = config.MemoryModel, MaxTokens = 500 };
        var promptMessages = new List<Message> { new Message { Content = fullPrompt, IsUser = true } };
        var summary = await provider.SendMessageAsync(requestConfig, promptMessages);

        Logger.LogInfo(summary);

        if (string.IsNullOrEmpty(config.VectorModel))
        {
            Logger.LogInfo("No vector model configured, skipping embedding");
            MarkMemorized(unmemoized);
            return false;
        }

        var paragraphs = summary
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (paragraphs.Count == 0)
        {
            MarkMemorized(unmemoized);
            return false;
        }

        Logger.LogInfo($"Extracted {paragraphs.Count} paragraphs from summary");

        var vectorProvider = _vectorProviderResolver.GetProviderForModel(config.VectorModel);
        var vectorStorage = _vectorStorageResolver.GetStorage(config.SelectedVectorStorage);

        var successfulStores = 0;
        foreach (var paragraph in paragraphs)
        {
            try
            {
                Logger.LogInfo($"Embedding paragraph: {paragraph.Substring(0, Math.Min(50, paragraph.Length))}...");
                var embedding = await vectorProvider.GetEmbeddingAsync(paragraph);
                Logger.LogInfo($"Storing memory with {embedding.Length}-dimensional embedding");
                await vectorStorage.StoreMemoryAsync(paragraph, embedding);
                successfulStores++;
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"Failed to store memory: {ex.Message}");
            }
        }

        if (successfulStores > 0)
        {
            Logger.LogInfo($"Successfully stored {successfulStores}/{paragraphs.Count} memories");
            MarkMemorized(unmemoized);
            return true;
        }

        Logger.LogInfo("Failed to store any memories, keeping messages unmarked for retry");
        return false;
    }

    protected IReadOnlyList<Message> SliceContext(IReadOnlyList<Message> history, int maxMessages)
    {
        return history.Count <= maxMessages
            ? history
            : history.Skip(history.Count - maxMessages).ToList();
    }

    private void MarkMemorized(List<Message> messages)
    {
        messages.Where(m => !m.IsMemorized).ToList().ForEach(m => m.IsMemorized = true);
    }
}
