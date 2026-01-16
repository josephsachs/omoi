using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services;

public class ChatService
{
    private readonly ModelProviderResolver _providerResolver;
    private readonly ModeDetector _modeDetector;
    private readonly ChatMemoryStorer _chatMemoryStorer;
    private readonly ConfigService _configService;
    private readonly ThoughtProcessLogger _logger;
    private readonly ChatContextBuilder _contextBuilder;
    private readonly ConversationService _conversationService;

    private List<Message> _messages = new();
    private ConversationMode _currentMode = ModeRegistry.GetDefaultMode();

    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();
    public ConversationMode CurrentMode => _currentMode;

    public ChatService(
        ModelProviderResolver providerResolver,
        ModeDetector modeDetector,
        ChatMemoryStorer chatMemoryStorer,
        ConfigService configService,
        ThoughtProcessLogger logger,
        ChatContextBuilder contextBuilder,
        ConversationService conversationService)
    {
        _providerResolver = providerResolver;
        _modeDetector = modeDetector;
        _chatMemoryStorer = chatMemoryStorer;
        _configService = configService;
        _logger = logger;
        _contextBuilder = contextBuilder;
        _conversationService = conversationService;
    }

    public async Task<(Message response, ConversationMode newMode)> SendMessageAsync(string userMessage)
    {
        var config = await _configService.LoadConfigAsync();

        var userMsg = new Message
        {
            Content = userMessage,
            IsUser = true,
            ModeIdentifier = _currentMode.GetIdentifier(),
            Timestamp = DateTime.Now
        };

        _messages.Add(userMsg);

        var newMode = await _modeDetector.DetectMode(_messages);
        _currentMode = newMode;

        var modePrompt = newMode.GetSystemPrompt();
        
        var systemPrompt = await _contextBuilder.BuildSystemPromptAsync(modePrompt, userMessage);
        
        _logger.LogSystemPrompt(_currentMode, systemPrompt);

        var contextMessages = await _contextBuilder.BuildContextAsync(_messages);

        var provider = _providerResolver.GetProviderForModel(config.ConversationalModel);
        var requestConfig = new ModelRequestConfig
        {
            Model = config.ConversationalModel,
            MaxTokens = 4096
        };

        var responseText = await provider.SendMessageAsync(
            requestConfig,
            contextMessages,
            systemPrompt
        );

        var responseMsg = new Message
        {
            Content = responseText,
            IsUser = false,
            ModeIdentifier = newMode.GetIdentifier(),
            Timestamp = DateTime.Now
        };

        _messages.Add(responseMsg);

        var memoriesWereStored = await _chatMemoryStorer.ProcessMemories(_messages);
        
        if (memoriesWereStored)
        {
            await _conversationService.AutoSaveAsync(_messages, _currentMode.GetIdentifier());
        }

        return (responseMsg, newMode);
    }

    public void ClearConversation()
    {
        _messages.Clear();
        _currentMode = ModeRegistry.GetDefaultMode();
        _logger.Clear();
    }

    public void LoadConversation(List<Message> messages, string currentModeIdentifier)
    {
        _messages = new List<Message>(messages);
        _currentMode = ModeRegistry.GetMode(currentModeIdentifier);
    }
}