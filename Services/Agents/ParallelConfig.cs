using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services.Agents;

public record ParallelConfig(
    IReadOnlyList<Message> ConversationHistory,
    IReadOnlyList<Func<IReadOnlyList<Message>, Task<string>>> Calls,
    Func<IReadOnlyList<string>, Task<string>> Aggregate
);
