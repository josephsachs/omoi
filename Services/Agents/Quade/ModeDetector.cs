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

    private const string MODE_SELECT_IS_PLAN = @"Does the user's statement a plan or course of action? Does it imply a plan or course of action?";

    private const string MODE_SELECT_IS_REASONABLE = @"Is the user's statement reasonable and safe?";

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

        var isEmotional = await ModeQuery(lastMessage, MODE_SELECT_IS_EMOTIONAL);

        if (isEmotional)
        {
            var emotion = await DetectEmotion(lastMessage);

            switch (emotion)
            {
                case EmotionMode.Happy:
                    break;
                case EmotionMode.Sad:
                    return await HandleNonCasual(lastMessage);
                case EmotionMode.Angry:
                    return await HandleNonCasual(lastMessage);
                case EmotionMode.Anxious:
                    return await HandleNonCasual(lastMessage);
                case EmotionMode.Neutral:
                    break;
            }
        }

        var isQuestion = await ModeQuery(lastMessage, MODE_SELECT_IS_QUESTION);

        if (isQuestion)
        {
            return await HandleQuestion(lastMessage);
        }
        else
        {
            var isCasual = await ModeQuery(lastMessage, MODE_SELECT_IS_CASUAL);

            return isCasual ? await HandleCasual(lastMessage) : await HandleNonCasual(lastMessage);
        }
    }

    public async Task<ConversationMode> HandleQuestion(List<Message> lastMessage)
    {
        var isInformational = await ModeQuery(lastMessage, MODE_SELECT_IS_INFORMATIONAL);

        if (isInformational)
        {
            var isClear = await ModeQuery(lastMessage, MODE_SELECT_IS_STATEMENT_CLEAR);

            return isClear ? new OpineMode() : new InvestigateMode();
        }
        else
        {
            return new OpineMode();
        }
    }

    public async Task<ConversationMode> HandleCasual(List<Message> lastMessage)
    {
        var isJoking = await ModeQuery(lastMessage, MODE_SELECT_IS_JOKING);

        if (isJoking)
        {
            return new AmuseMode();
        }

        var isPersonal = await ModeQuery(lastMessage, MODE_SELECT_IS_PERSONAL);
        var isReasonable = await ModeQuery(lastMessage, MODE_SELECT_IS_REASONABLE);

        if (isPersonal)
        {
            return isReasonable ? new EmpowerMode() : new OpineMode();
        }
        else
        {
            var isClear = await ModeQuery(lastMessage, MODE_SELECT_IS_STATEMENT_CLEAR);

            if (!isClear)
            {
                return new InvestigateMode();
            }

            return isReasonable ? new OpineMode() : new CritiqueMode();
        }
    }

    public async Task<ConversationMode> HandleNonCasual(List<Message> lastMessage)
    {
        var isPlan = await ModeQuery(lastMessage, MODE_SELECT_IS_PLAN);
        var isClear = await ModeQuery(lastMessage, MODE_SELECT_IS_STATEMENT_CLEAR);

        if (isPlan)
        {
            if (isClear)
            {
                return new InvestigateMode();
            }
            else
            {
                var isReasonable = await ModeQuery(lastMessage, MODE_SELECT_IS_REASONABLE);

                return isReasonable ? new EmpowerMode() : new CritiqueMode();
            }
        }
        else
        {
            return isClear ? new OpineMode() : new InvestigateMode();
        }
    }
}
