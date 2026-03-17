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

        Write one paragraph per distinct item, capturing the memorable crux, at most a couple of sentences. Past tense. Separate paragraphs with a blank line. Max 2 items.

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
        var history = new List<Message>(input.History);

        var newMode = await _modeDetector.DetectMode(history);
        var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(newMode.GetSystemPrompt(), input.UserMessage);
        Logger.LogSystemPrompt(newMode, systemPrompt);

        var contextMessages = await _contextBuilder.BuildContextAsync(history);

        var responseText = newMode switch
        {
            OpineMode => await RunOpineDialogue(contextMessages, input.UserMessage, systemPrompt),
            EmpowerMode => await RunEmpowerDialogue(contextMessages, input.UserMessage, systemPrompt),
            CritiqueMode => await RunCritiqueDialogue(contextMessages, input.UserMessage, systemPrompt),
            InvestigateMode => await RunInvestigateChain(contextMessages, input.UserMessage, systemPrompt),
            _ => await Generate(ModelType.Conversational, contextMessages, systemPrompt)
        };

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

    private async Task<string> RunOpineDialogue(
        List<Message> conversationHistory, string userMessage, string systemPrompt)
    {
        var advocate = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You are sincerely arguing for one side of the question. " +
                "Develop the strongest version of that position with genuine conviction."),
            "Advocate");

        var skeptic = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You are the opposing voice. Challenge what was just argued, " +
                "steelman the other side, and surface what the advocate is glossing over."),
            "Skeptic");

        return await RunDialogue(new DialogueConfig(
            Participants: [advocate, skeptic],
            ConversationHistory: conversationHistory,
            InitialMessage: userMessage,
            NextSpeaker: history => history.Count % 2 == 1 ? advocate : skeptic,
            ShouldTerminate: async history =>
            {
                if (history.Count < 5) return false;
                var verdict = await Query(
                    "Has each side made its strongest case and been meaningfully challenged? Yes or No.",
                    history);
                return verdict.Trim().StartsWith("Yes", StringComparison.OrdinalIgnoreCase);
            },
            ExtractOutput: async history => await Generate(
                ModelType.Conversational,
                DialogueAsContext(history),
                systemPrompt + "\n\nBased on the above exchange, synthesize a balanced response for the user. Do not reveal the internal dialogue.")
        ));
    }

    private async Task<string> RunEmpowerDialogue(
        List<Message> conversationHistory, string userMessage, string systemPrompt)
    {
        var meritFinder = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You find what is genuinely strong and promising about the idea or situation presented. " +
                "Not cheerleading — identify the real merit, what actually works, what has potential."),
            "MeritFinder");

        var deepReader = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You read the idea deeply and follow it wherever it goes, without advocating for or against it. " +
                "Notice implications, connections, what this touches that might not be obvious."),
            "DeepReader");

        return await RunDialogue(new DialogueConfig(
            Participants: [meritFinder, deepReader],
            ConversationHistory: conversationHistory,
            InitialMessage: userMessage,
            NextSpeaker: history => history.Count % 2 == 1 ? meritFinder : deepReader,
            ShouldTerminate: async history =>
            {
                if (history.Count < 5) return false;
                var verdict = await Query(
                    "Have both the genuine strengths and the deeper implications been fully explored? Yes or No.",
                    history);
                return verdict.Trim().StartsWith("Yes", StringComparison.OrdinalIgnoreCase);
            },
            ExtractOutput: async history => await Generate(
                ModelType.Conversational,
                DialogueAsContext(history),
                systemPrompt + "\n\nBased on this internal exchange, craft an encouraging response that feels earned and grounded. Do not reveal the internal dialogue.")
        ));
    }

    private async Task<string> RunCritiqueDialogue(
        List<Message> conversationHistory, string userMessage, string systemPrompt)
    {
        var defender = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You make the strongest case for the idea being discussed. Defend it genuinely."),
            "Defender");

        var critic = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You critically examine the idea. Find its weaknesses, challenge its assumptions, " +
                "identify what is being overlooked or oversimplified."),
            "Critic");

        return await RunDialogue(new DialogueConfig(
            Participants: [defender, critic],
            ConversationHistory: conversationHistory,
            InitialMessage: userMessage,
            NextSpeaker: history => history.Count % 2 == 1 ? defender : critic,
            ShouldTerminate: async history =>
            {
                if (history.Count < 5) return false;
                if (history.Count % 2 == 0) return false; // only terminate after critic speaks
                var verdict = await Query(
                    "Has the critique been thoroughly developed with the strongest points made? Yes or No.",
                    history);
                return verdict.Trim().StartsWith("Yes", StringComparison.OrdinalIgnoreCase);
            },
            ExtractOutput: async history => await Generate(
                ModelType.Conversational,
                DialogueAsContext(history),
                systemPrompt + "\n\nBased on this internal critique, craft a response that honestly addresses the weaknesses. Do not reveal the internal dialogue.")
        ));
    }

    private async Task<string> RunInvestigateChain(
        List<Message> conversationHistory, string userMessage, string systemPrompt)
    {
        return await RunChain(new ChainConfig(
            ConversationHistory: conversationHistory,
            InitialInput: userMessage,
            Steps:
            [
                async (input, history) => await Generate(
                    ModelType.Thought,
                    history,
                    $"Identify 2-3 specific aspects of the following that are worth investigating or may be unclear:\n\n{input}"),

                async (aspects, history) => await Generate(
                    ModelType.Thought,
                    history,
                    $"Investigate each of these aspects thoughtfully:\n\n{aspects}"),

                async (investigation, history) => await Generate(
                    ModelType.Conversational,
                    history,
                    systemPrompt + $"\n\nDrawing on this analysis:\n\n{investigation}\n\nRespond to the user.")
            ]
        ));
    }
}
