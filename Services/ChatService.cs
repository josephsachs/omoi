using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;
using Omoi.Services.Agents;

namespace Omoi.Services;

public class ChatService
{
    private readonly Agent _agent;
    private readonly ConversationService _conversationService;
    private readonly ThoughtProcessLogger _logger;

    private List<Message> _messages = new();
    private string _currentCharacter = ModeRegistry.GetDefaultMode().GetIdentifier();

    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();
    public ConversationMode CurrentMode => ModeRegistry.GetMode(_currentCharacter);

    public ChatService(
        Agent agent,
        ConversationService conversationService,
        ThoughtProcessLogger logger)
    {
        _agent = agent;
        _conversationService = conversationService;
        _logger = logger;
    }

    public async Task<(Message response, ConversationMode newMode)> SendMessageAsync(string userMessage)
    {
        var userMsg = new Message
        {
            Content = userMessage,
            IsUser = true,
            ModeIdentifier = _currentCharacter,
            Timestamp = DateTime.Now
        };

        _messages.Add(userMsg);

        var result = await _agent.Handle(new AgentInput(_messages.AsReadOnly(), userMessage));

        _currentCharacter = result.Character;

        var responseMsg = new Message
        {
            Content = result.Content,
            IsUser = false,
            ModeIdentifier = result.Character,
            Timestamp = DateTime.Now
        };

        _messages.Add(responseMsg);

        if (result.MemoriesStored)
            await _conversationService.AutoSaveAsync(_messages, _currentCharacter);

        return (responseMsg, ModeRegistry.GetMode(_currentCharacter));
    }

    public void ClearConversation()
    {
        _messages.Clear();
        _currentCharacter = ModeRegistry.GetDefaultMode().GetIdentifier();
        _logger.Clear();
    }

    public void LoadConversation(List<Message> messages, string currentModeIdentifier)
    {
        _messages = new List<Message>(messages);
        _currentCharacter = currentModeIdentifier;
    }
}
