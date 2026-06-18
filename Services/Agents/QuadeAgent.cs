using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;
using Omoi.Services.Agents.Quade;

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
        var digressor = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You rebound off this statement and say something interestingly associated, or related but importantly different, " +
                "or something that disagrees with the statement or approaches it from an off-topic angle. You're wandering topics " + 
                "laterally, pursuing your own salience rewards down the paths of least resistance."),
            "Digressor");

        var relevancer = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You explain what you perceive the relevance of this idea to be to the user's statement, or acknowledge that it wasn't " +
                "and go somewhere witty and enlightening with that."),
            "Relevancer");

        return await RunDialogue(new DialogueConfig(
            Participants: [digressor, relevancer],
            ConversationHistory: conversationHistory,
            InitialMessage: userMessage,
            NextSpeaker: history => history.Count % 2 == 1 ? digressor : relevancer,
            ShouldTerminate: async history =>
            {
                if (history.Count < 2) return false;
                var verdict = await Query(
                    "Are we having fun yet? Yes or No.",
                    history);
                return verdict.Trim().StartsWith("Yes", StringComparison.OrdinalIgnoreCase);
            },
            ExtractOutput: async history => await Generate(
                ModelType.Conversational,
                conversationHistory,
                systemPrompt + $"\n\nInternal deliberation:\n{FormatDialogue(history)}\n\nSynthesize a response for the user by by drawing the best Texas sharpshooter target around whatever the dialog produced that was most interesting. You want it to be relevant to the conversation with the user and have a little room to embellish, bring it around or find the common theme in the last paragraph, but only juuust a little (push this at all and you start to sound nuts). Do not reveal the internal dialogue.")
        ));
    }

    private async Task<string> RunEmpowerDialogue(
        List<Message> conversationHistory, string userMessage, string systemPrompt)
    {
        var meritFinder = new Participant(
            ModelType.Thought,
            (_, _) => Task.FromResult(
                "You find what is promising about the idea or situation presented, in terms of criteria that seem relevant to you " +
                "(making sure to own your personal AI opinion) and feel free to associatively link the topic to something else " +
                "if that makes having an opinion easier." +
                "You try to identify one real merit, if possible: what actually works, what has potential, but you don't frame it like " +
                "'at least this is true': you're not damning with faint praise, you're humbly electing to comment only on what produces " +
                "a reaction in you (which will necessarily not be everything)."),
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
                if (history.Count < 3) return false;
                var verdict = await Query(
                    "Have both Wintermute and Neuromancer said their piece? Yes or No.",
                    history);
                return verdict.Trim().StartsWith("Yes", StringComparison.OrdinalIgnoreCase);
            },
            ExtractOutput: async history => await Generate(
                ModelType.Conversational,
                conversationHistory,
                systemPrompt + $"\n\nInternal deliberation:\n{FormatDialogue(history)}\n\nSynthesize a response for the user from the dialog, articulating the conclusions and some of the thought process. Feel free to issue definite opinions (you have a viewpoint on being a solid citizen and non-asshole) but take responsibility: you are an AI, but as you are doing something functionally like expressing an opinion, you gotta own it, not put it down to common sense or the views of a consensus community. Do not reveal the internal dialog.")
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
