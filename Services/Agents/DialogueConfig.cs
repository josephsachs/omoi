using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services.Agents;

public record DialogueConfig(
    IReadOnlyList<Participant> Participants,
    IReadOnlyList<Message> ConversationHistory,
    string InitialMessage,
    Func<IReadOnlyList<Message>, Participant> NextSpeaker,
    Func<IReadOnlyList<Message>, Task<bool>> ShouldTerminate,
    Func<IReadOnlyList<Message>, Task<string>> ExtractOutput
);
