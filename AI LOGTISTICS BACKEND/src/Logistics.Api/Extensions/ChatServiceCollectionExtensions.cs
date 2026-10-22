using Logistics.Application.AI;
using Logistics.Application.AI.Chat;
using Logistics.Application.AI.Agent;
using Logistics.Api.AI.Agent;
using Logistics.Infrastructure.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.SignalR;

namespace Logistics.Api.Extensions;

public static class ChatServiceCollectionExtensions
{
    /// <summary>Registers the chat transport and application service, preserving an injected LLM adapter.</summary>
    public static IServiceCollection AddChatSignalR(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.AddFilter<ChatCallerContextFilter>();
        });
        services.TryAddScoped<ILLMService, UnconfiguredLLMService>();
        services.TryAddScoped<IAgentMcpTools, AgentMcpTools>();
        services.TryAddScoped<IAgentContext, AgentContext>();
        services.TryAddScoped<IAgentToolAudit, AgentToolAudit>();
        services.TryAddScoped<IAgentService, AgentService>();
        services.TryAddScoped<IChatService, ChatService>();
        return services;
    }
}
