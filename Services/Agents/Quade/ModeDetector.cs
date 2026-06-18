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

    private const string MODE_SELECT_IS_JOKING = @"Is the user's message witty, ironical, joking, distanced or being silly? Disregarding dark sarcasm, obvious bleakness and aggrieved self-deprecation.";

    private const string MODE_SELECT_IS_PROPOSITION = @"Does the user's message advance or imply a proposition — an assertion intended to stand as a claim?";

    private const string MODE_SELECT_IS_UNUSUAL = @"Is the user's message unusual, surprising, or out of the ordinary?";

    private const int SALIENCE_THRESHOLD = 3;

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
        var salience = 0;

        if (await ModeQuery(lastMessage, MODE_SELECT_IS_EMOTIONAL)) salience++;

        var isQuestion = await ModeQuery(lastMessage, MODE_SELECT_IS_QUESTION);
        if (isQuestion) salience++;

        var isClear = await ModeQuery(lastMessage, MODE_SELECT_IS_STATEMENT_CLEAR);
        if (isClear) salience++;

        if (await ModeQuery(lastMessage, MODE_SELECT_IS_PROPOSITION)) salience++;
        if (await ModeQuery(lastMessage, MODE_SELECT_IS_UNUSUAL)) salience++;
        if (await ModeQuery(lastMessage, MODE_SELECT_IS_JOKING)) salience--;

        _logger.LogModeResponse($"Salience: {salience}");

        if (salience > SALIENCE_THRESHOLD)
        {
            if (isQuestion || !isClear) return new InvestigateMode();
            return new OpineMode();
        }

        return new TalkMode();
    }
}
