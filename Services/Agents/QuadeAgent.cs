using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services.Agents;

public class QuadeAgent : Agent
{
    private readonly ModeDetector _modeDetector;
    private readonly ChatContextBuilder _contextBuilder;

    private const string MemoryInstruction = """
        Extract idiosyncratic ideas, insights, or interesting concepts that both user and assistant recognized. Skip chitchat. Skip transient events, or anything that may likely not apply out of context. Ignore jokes, especially shitposts and wordplay. Err on the side of parsimony. Retain what has explanatory power. Skip most facts about the user, preserve those that are obviously personal talismans or have thoroughgoing thematic relevance. If nothing meets the criteria, return blank.

        When noting assistant's own insights, note that the assistant originated them, but avoid recapping exchanges.

        Write one paragraph per distinct item, capturing the memorable crux, at most a couple of sentences. Separate paragraphs with a blank line. Max 2 items.

        Do not use headings, bullet points, numbered lists, bold text, or any other formatting. Do not prefix paragraphs with labels like 'Topic:' or 'Memory:'. Just plain prose.
        """;

    public QuadeAgent(
        ModelProviderResolver providerResolver,
        VectorProviderResolver vectorProviderResolver,
        VectorStorageResolver vectorStorageResolver,
        ConfigService configService,
        ThoughtProcessLogger logger,
        ModeDetector modeDetector,
        ChatContextBuilder contextBuilder)
        : base(providerResolver, vectorProviderResolver, vectorStorageResolver, configService, logger)
    {
        _modeDetector = modeDetector;
        _contextBuilder = contextBuilder;
    }

    public override async Task<AgentResult> Handle(AgentInput input)
    {
        var config = await ConfigService.LoadConfigAsync();
        var history = new List<Message>(input.History);

        var newMode = await _modeDetector.DetectMode(history);
        var modePrompt = newMode.GetSystemPrompt();

        var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(modePrompt, input.UserMessage);
        Logger.LogSystemPrompt(newMode, systemPrompt);

        var contextMessages = await _contextBuilder.BuildContextAsync(history);

        var provider = ProviderResolver.GetProviderForModel(config.ConversationalModel);
        var requestConfig = new ModelRequestConfig
        {
            Model = config.ConversationalModel,
            MaxTokens = 4096
        };

        var responseText = await provider.SendMessageAsync(requestConfig, contextMessages, systemPrompt);

        history.Add(new Message
        {
            Content = responseText,
            IsUser = false,
            ModeIdentifier = newMode.GetIdentifier(),
            Timestamp = DateTime.Now
        });

        var memoriesStored = await StoreMemories(history, MemoryInstruction);

        return new AgentResult(responseText, newMode.GetIdentifier(), memoriesStored);
    }
}
