using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services;

public class ChatContextBuilder
{
    private readonly VectorProviderResolver _vectorProviderResolver;
    private readonly VectorStorageResolver _vectorStorageResolver;
    private readonly ConfigService _configService;
    private readonly ThoughtProcessLogger _logger;

    public ChatContextBuilder(
        VectorProviderResolver vectorProviderResolver,
        VectorStorageResolver vectorStorageResolver,
        ConfigService configService,
        ThoughtProcessLogger logger)
    {
        _vectorProviderResolver = vectorProviderResolver;
        _vectorStorageResolver = vectorStorageResolver;
        _configService = configService;
        _logger = logger;
    }

    public async Task<List<Message>> BuildContextAsync(List<Message> allMessages)
    {
        var config = await _configService.LoadConfigAsync();
        var maxContextMessages = config.MaxContextMessages;

        if (allMessages.Count <= maxContextMessages)
        {
            return allMessages;
        }

        return allMessages.TakeLast(maxContextMessages).ToList();
    }

    public async Task<string> BuildSystemPromptAsync(string modePrompt, string userMessage)
    {
        var config = await _configService.LoadConfigAsync();

        var personalityPrompt = config.PersonalityPrompt;

        var systemPrompt = $"<instructions>\n{personalityPrompt}\n{modePrompt}\n</instructions>";

        if (string.IsNullOrEmpty(config.VectorModel) || 
            string.IsNullOrEmpty(config.MemoryModel))
        {
            _logger.LogInfo("Memory system not configured, skipping memory retrieval");
            return systemPrompt;
        }

        try
        {
            var vectorProvider = _vectorProviderResolver.GetProviderForModel(config.VectorModel);
            var embedding = await vectorProvider.GetEmbeddingAsync(userMessage, config.VectorModel);

            var storage = _vectorStorageResolver.GetStorage(config.SelectedVectorStorage);
            var topK = config.TopKMemories;
            var threshold = config.SimilarityThreshold;
            var memories = await storage.SearchSimilarMemoriesAsync(embedding, topK, threshold);

            if (memories.Count == 0)
            {
                _logger.LogInfo("No memories found for current query");
                return systemPrompt;
            }

            _logger.LogInfo($"Retrieved {memories.Count} memories:");
            foreach (var memory in memories)
            {
                _logger.LogInfo($"  - [{memory.Similarity:F3}] {TruncateForLog(memory.Content)}");
            }

            var memoryXml = FormatMemoriesAsXml(memories);
            systemPrompt += $"\n\n{memoryXml}";

            return systemPrompt;
        }
        catch (System.Exception ex)
        {
            _logger.LogInfo($"Memory retrieval failed: {ex.Message}");
            return systemPrompt;
        }
    }

    private string FormatMemoriesAsXml(List<Memory> memories)
    {
        var lines = new List<string> { "<memories>" };
        
        foreach (var memory in memories)
        {
            lines.Add($"<memory>{memory.Content}</memory>");
        }

        lines.Add("</memories>");

        return string.Join("\n", lines);
    }

    private string TruncateForLog(string content, int maxLength = 80)
    {
        if (content.Length <= maxLength)
            return content;
        
        return content.Substring(0, maxLength) + "...";
    }
}