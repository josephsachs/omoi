using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services.Agents;

public record ChainConfig(
    IReadOnlyList<Message> ConversationHistory,
    string InitialInput,
    IReadOnlyList<Func<string, IReadOnlyList<Message>, Task<string>>> Steps
);
