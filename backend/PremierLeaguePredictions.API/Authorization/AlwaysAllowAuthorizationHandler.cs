using Microsoft.AspNetCore.Authorization;

namespace PremierLeaguePredictions.API.Authorization;

/// <summary>
/// Authorization handler that allows all requests - ONLY FOR DEVELOPMENT
/// </summary>
public class AlwaysAllowAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements.ToList())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
