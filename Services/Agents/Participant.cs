using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Omoi.Models;

namespace Omoi.Services.Agents;

public record Participant(
    ModelType ModelType,
    Func<IReadOnlyList<Message>, IReadOnlyList<Message>, Task<string>> SystemPromptFactory,
    string? Name = null
);
