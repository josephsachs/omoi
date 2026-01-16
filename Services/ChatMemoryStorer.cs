using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services;

public class ChatMemoryStorer
{
    private readonly ModelProviderResolver _providerResolver;
    private readonly VectorProviderResolver _vectorProviderResolver;
    private readonly VectorStorageResolver _vectorStorageResolver;
    private readonly ThoughtProcessLogger _logger;
    private readonly ConfigService _configService;

    private const int MEMORY_STORE_INTERVAL = 4;

    public ChatMemoryStorer(
        ModelProviderResolver providerResolver,
        VectorProviderResolver vectorProviderResolver,
        VectorStorageResolver vectorStorageResolver,
        ThoughtProcessLogger logger,
        ConfigService configService)
    {
        _providerResolver = providerResolver;
        _vectorProviderResolver = vectorProviderResolver;
        _vectorStorageResolver = vectorStorageResolver;
        _logger = logger;
        _configService = configService;
    }

    public async Task<bool> ProcessMemories(List<Message> allMessages)
    {
        var unmemoizedMessages = GetUnmemoizedMessages(allMessages);
        
        if (!ShouldProcessMemories(unmemoizedMessages))
            return false;

        _logger.LogInfo($"Processing {unmemoizedMessages.Count} messages into memory...");

        var transcript = FormatAsTranscript(unmemoizedMessages);
        var summary = await GenerateMemorySummary(transcript);
        var paragraphs = ExtractParagraphs(summary);
        
        if (paragraphs.Count == 0)
        {
            MarkMessagesAsMemorized(allMessages);
            return false;
        }

        _logger.LogInfo($"Extracted {paragraphs.Count} paragraphs from summary");

        var config = await _configService.LoadConfigAsync();
        
        if (string.IsNullOrEmpty(config.VectorModel))
        {
            _logger.LogInfo("No vector model configured, skipping embedding");
            MarkMessagesAsMemorized(allMessages);
            return false;
        }

        var vectorProvider = _vectorProviderResolver.GetProviderForModel(config.VectorModel);
        var vectorStorage = _vectorStorageResolver.GetStorage(config.SelectedVectorStorage);

        var successfulStores = 0;
        foreach (var paragraph in paragraphs)
        {
            try
            {
                _logger.LogInfo($"Embedding paragraph: {paragraph.Substring(0, Math.Min(50, paragraph.Length))}...");
                
                var embedding = await vectorProvider.GetEmbeddingAsync(paragraph);
                
                _logger.LogInfo($"Storing memory with {embedding.Length}-dimensional embedding");
                
                await vectorStorage.StoreMemoryAsync(paragraph, embedding);
                
                successfulStores++;
            }
            catch (Exception ex)
            {
                _logger.LogInfo($"Failed to store memory: {ex.Message}");
            }
        }

        if (successfulStores > 0)
        {
            _logger.LogInfo($"Successfully stored {successfulStores}/{paragraphs.Count} memories");
            MarkMessagesAsMemorized(allMessages);
            return true;
        }
        else
        {
            _logger.LogInfo("Failed to store any memories, keeping messages unmarked for retry");
            return false;
        }
    }

    private List<Message> GetUnmemoizedMessages(List<Message> allMessages)
    {
        return allMessages.Where(m => !m.IsMemorized).ToList();
    }

    private bool ShouldProcessMemories(List<Message> unmemoizedMessages)
    {
        return unmemoizedMessages.Count >= MEMORY_STORE_INTERVAL;
    }

    private string FormatAsTranscript(List<Message> messages)
    {
        return string.Join("\n\n", messages.Select(m =>
        {
            var participant = m.IsUser ? "User" : "Assistant";
            return $"{participant}: \"{m.Content}\"";
        }));
    }

    private async Task<string> GenerateMemorySummary(string transcript)
    {
        var config = await _configService.LoadConfigAsync();
        var provider = _providerResolver.GetProviderForModel(config.MemoryModel);

        var requestConfig = new ModelRequestConfig
        {
            Model = config.MemoryModel,
            MaxTokens = 500
        };

        var prompt = BuildMemoryPrompt(transcript);
        var promptMessages = new List<Message>
        {
            new Message
            {
                Content = prompt,
                IsUser = true
            }
        };

        var response = await provider.SendMessageAsync(requestConfig, promptMessages);

        _logger.LogInfo($"{response}");
        
        return response;
    }

    private string BuildMemoryPrompt(string transcript)
    {
        return $$"""
            Here is a conversation transcript:

            {{transcript}}

            Extract idiosyncratic ideas, insights, or interesting concepts that both user and assistant recognized.  Skip chitchat. Skip transient events, or anything that may likely not apply out of context. Ignore jokes, especially shitposts and wordplay. Err on the side of parsimony. Retain what has explanatory power. Skip most facts about the user, preserve those that are obviously personal talismans or have thoroughgoing thematic relevance. If nothing meets the criteria, return blank.
            
            When noting assistant's own insights, note that the assistant originated them, but avoid recapping exchanges.

            Write one paragraph per distinct item, capturing the memorable crux, at most a couple of sentences. Separate paragraphs with a blank line. Max 2 items.

            Do not use headings, bullet points, numbered lists, bold text, or any other formatting. Do not prefix paragraphs with labels like 'Topic:' or 'Memory:'. Just plain prose.
            """;
    }

    private List<string> ExtractParagraphs(string summary)
    {
        if (summary.Contains("**") || summary.Contains("- ") || summary.Contains("1."))
        {
            _logger.LogInfo("Warning: Memory summary contains formatting markers");
        }

        var paragraphs = summary
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
        
        return paragraphs;
    }

    private void MarkMessagesAsMemorized(List<Message> allMessages)
    {
        var count = allMessages.Count(m => !m.IsMemorized);
        
        allMessages
            .Where(m => !m.IsMemorized)
            .ToList()
            .ForEach(m => m.IsMemorized = true);
    }
}