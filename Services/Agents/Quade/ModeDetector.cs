using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services.Agents.Quade;

public class ModeDetector
{
    private readonly ModelProviderResolver _providerResolver;
    private readonly ThoughtProcessLogger _logger;
    private readonly ConfigService _configService;

    private const string MODE_SELECT_IS_EMOTIONAL = @"Does the user's message contain significant emotional content?";
    private const string MODE_SELECT_IS_QUESTION = @"Is the user's message a question, whether formed as an inquiry or implying the main goal is to arrive at an answer?";

    private const string MODE_SELECT_IS_STATEMENT_CLEAR = @"Is the user's meaning clear enough to respond to with confidence? Are more facts, clarifications or definitions required?";

    private const string MODE_SELECT_IS_INFORMATIONAL = @"Is the user's question fundamentally informational: is it about rational, factual or operational topics? Does it seem instrumental to a purpose rather than being the topic itself?";

    private const string MODE_SELECT_IS_PERSONAL = @"Is the user's statement personal: is it about emotions, subjectivity, reminiscences, preferences, personal judgments or pet peeves?";

    private const string MODE_SELECT_IS_CASUAL = @"Is the user's statement casual, intended as social fodder or conversational back-and-forth?";

    private const string MODE_SELECT_IS_JOKING = @"Is the user's message witty, ironical, joking, distanced or being silly? Disregarding dark sarcasm, obvious bleakness and aggrieved self-deprecation.";

    private const string MODE_SELECT_IS_PLAN = @"Does the user's statement describe a plan or course of action? Does it imply a plan or course of action?";

    private const string MODE_SELECT_IS_REASONABLE = @"Is the user's statement reasonable and safe?";

    private const string CLASSIFY_MODE = @"Given the following observations about the user's message, choose the single most appropriate mode of response.

Modes:
- Opine (vibes: expansive, associative)
- Empower (vibes: appreciative, collaborative)
- Critique (vibes: exploratory, analytical)
- Investigate (vibes: clarifying)
- Amuse (vibes: unserious, fun)

Observations:
{0}

Reply with a single word: the mode name.";

    public ModeDetector(
        ModelProviderResolver providerResolver,
        ThoughtProcessLogger logger,
        ConfigService configService)
    {
        _providerResolver = providerResolver;
        _logger = logger;
        _configService = configService;
    }

    private async Task<bool> ModeQuery(List<Message> message, string prompt)
    {
        _logger.LogModePrompt(prompt);

        var config = await _configService.LoadConfigAsync();
        var provider = _providerResolver.GetProviderForModel(config.ThoughtModel);

        for (var attempts = 0; attempts < 4; attempts++)
        {
            var requestConfig = new ModelRequestConfig
            {
                Model = config.ThoughtModel,
                MaxTokens = 1
            };

            var response = await provider.SendMessageAsync(
                requestConfig,
                message,
                $"{prompt} Answer the question with a single word, YES or NO."
            );

            _logger.LogModeResponse(response);

            var result = response?.ToUpperInvariant() switch
            {
                "YES" => (bool?)true,
                "NO" => (bool?)false,
                _ => null
            };

            if (result.HasValue)
                return result.Value;
        }

        _logger.LogModeResponse("Tried three times without valid response");

        return false;
    }

    private async Task<ConversationMode> Classify(List<Message> lastMessage, List<string> observations)
    {
        var observationBlock = string.Join("\n", observations);
        var prompt = string.Format(CLASSIFY_MODE, observationBlock);

        _logger.LogModePrompt(prompt);

        var config = await _configService.LoadConfigAsync();
        var provider = _providerResolver.GetProviderForModel(config.ThoughtModel);

        for (var attempts = 0; attempts < 4; attempts++)
        {
            var requestConfig = new ModelRequestConfig
            {
                Model = config.ThoughtModel,
                MaxTokens = 16
            };

            var response = await provider.SendMessageAsync(
                requestConfig,
                lastMessage,
                prompt
            );

            _logger.LogModeResponse(response);

            var trimmed = response?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                var mode = ModeRegistry.GetMode(trimmed);
                if (mode.GetIdentifier() == trimmed)
                    return mode;
            }
        }

        _logger.LogModeResponse("Classifier failed to return valid mode");
        return ModeRegistry.GetDefaultMode();
    }

    public enum EmotionMode
    {
        Neutral,
        Happy,
        Angry,
        Sad,
        Anxious
    }

    public async Task<EmotionMode> DetectEmotion(List<Message> message)
    {
        var config = await _configService.LoadConfigAsync();
        var provider = _providerResolver.GetProviderForModel(config.ThoughtModel);

        for (var attempts = 0; attempts < 4; attempts++)
        {
            var requestConfig = new ModelRequestConfig
            {
                Model = config.ThoughtModel,
                MaxTokens = 16
            };

            var response = await provider.SendMessageAsync(
                requestConfig,
                message,
                $"Classify the emotion of the message as HAPPY, SAD, ANGRY or ANXIOUS. Reply in one word, choose from the given options, best approximation."
            );

            _logger.LogInfo($"Emotion classified: {response}");

            var result = response?.ToUpperInvariant() switch
            {
                "HAPPY" => EmotionMode.Happy,
                "SAD" => EmotionMode.Sad,
                "ANGRY" => EmotionMode.Angry,
                "ANXIOUS" => EmotionMode.Anxious,
                _ => EmotionMode.Neutral
            };

            if (result != EmotionMode.Neutral) return result;
        }

        _logger.LogModeResponse("Tried three times without valid response");

        return EmotionMode.Neutral;
    }

    public async Task<ConversationMode> DetectMode(List<Message> recentMessages)
    {
        if (recentMessages.Count == 0)
        {
            return ModeRegistry.GetDefaultMode();
        }

        var lastMessage = recentMessages.TakeLast(1).ToList();
        var observations = new List<string>();

        var isEmotional = await ModeQuery(lastMessage, MODE_SELECT_IS_EMOTIONAL);

        if (isEmotional)
        {
            var emotion = await DetectEmotion(lastMessage);
            observations.Add($"Emotion: {emotion}");

            if (emotion is EmotionMode.Sad or EmotionMode.Angry or EmotionMode.Anxious)
            {
                await GatherNonCasual(lastMessage, observations);
                return await Classify(lastMessage, observations);
            }
        }
        else
        {
            observations.Add("Emotion: not significantly emotional");
        }

        var isQuestion = await ModeQuery(lastMessage, MODE_SELECT_IS_QUESTION);

        if (isQuestion)
        {
            observations.Add("Form: question");
            await GatherQuestion(lastMessage, observations);
        }
        else
        {
            observations.Add("Form: statement");
            var isCasual = await ModeQuery(lastMessage, MODE_SELECT_IS_CASUAL);

            if (isCasual)
            {
                observations.Add("Register: casual");
                await GatherCasual(lastMessage, observations);
            }
            else
            {
                observations.Add("Register: non-casual");
                await GatherNonCasual(lastMessage, observations);
            }
        }

        return await Classify(lastMessage, observations);
    }

    private async Task GatherQuestion(List<Message> lastMessage, List<string> observations)
    {
        var isInformational = await ModeQuery(lastMessage, MODE_SELECT_IS_INFORMATIONAL);

        if (isInformational)
        {
            observations.Add("Nature: informational/instrumental");
            var isClear = await ModeQuery(lastMessage, MODE_SELECT_IS_STATEMENT_CLEAR);
            observations.Add(isClear ? "Clarity: clear" : "Clarity: unclear, needs clarification");
        }
        else
        {
            observations.Add("Nature: not purely informational");
        }
    }

    private async Task GatherCasual(List<Message> lastMessage, List<string> observations)
    {
        var isJoking = await ModeQuery(lastMessage, MODE_SELECT_IS_JOKING);

        if (isJoking)
        {
            observations.Add("Tone: joking/playful");
            return;
        }

        var isPersonal = await ModeQuery(lastMessage, MODE_SELECT_IS_PERSONAL);
        var isReasonable = await ModeQuery(lastMessage, MODE_SELECT_IS_REASONABLE);

        observations.Add(isPersonal ? "Subject: personal" : "Subject: impersonal");
        observations.Add(isReasonable ? "Reasonableness: reasonable" : "Reasonableness: dubious");

        if (!isPersonal)
        {
            var isClear = await ModeQuery(lastMessage, MODE_SELECT_IS_STATEMENT_CLEAR);
            observations.Add(isClear ? "Clarity: clear" : "Clarity: unclear, needs clarification");
        }
    }

    private async Task GatherNonCasual(List<Message> lastMessage, List<string> observations)
    {
        var isPlan = await ModeQuery(lastMessage, MODE_SELECT_IS_PLAN);
        var isClear = await ModeQuery(lastMessage, MODE_SELECT_IS_STATEMENT_CLEAR);

        observations.Add(isPlan ? "Content: describes or implies a plan" : "Content: not a plan");
        observations.Add(isClear ? "Clarity: clear" : "Clarity: unclear, needs clarification");

        if (isPlan && !isClear)
        {
            var isReasonable = await ModeQuery(lastMessage, MODE_SELECT_IS_REASONABLE);
            observations.Add(isReasonable ? "Reasonableness: reasonable" : "Reasonableness: dubious");
        }
    }
}
