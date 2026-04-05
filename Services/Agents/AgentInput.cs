using System.Collections.Generic;
using Omoi.Models;

namespace Omoi.Services.Agents;

public record AgentInput(IReadOnlyList<Message> History, string UserMessage);
