using Logistics.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace Logistics.Api.AI.Agent;

/// <summary>Transfers the authenticated HTTP caller into the SignalR invocation scope. No business rules.</summary>
public sealed class ChatCallerContextFilter : IHubFilter
{
    public ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var target = invocationContext.ServiceProvider.GetService<CallerContext>();
        var source = invocationContext.Context.GetHttpContext()?.RequestServices.GetService<CallerContext>();
        if (target is not null && source is not null && invocationContext.Context.User?.Identity?.IsAuthenticated == true)
        {
            target.Caller = source.Caller;
            target.HeaderFallbackAllowed = false;
        }
        return next(invocationContext);
    }
}
