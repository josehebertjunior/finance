using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FinanceApp.Api.Authorization;

public class IsOwnerOrAdminRequirement : IAuthorizationRequirement { }

public class IsOwnerOrAdminHandler : AuthorizationHandler<IsOwnerOrAdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, IsOwnerOrAdminRequirement requirement)
    {
        // Admins succeed
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Try to get route userId from HttpContext if available
        if (context.Resource is Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            if (httpContext.Request.RouteValues.TryGetValue("userId", out var uv))
            {
                var userId = uv?.ToString();
                var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(sub) && userId == sub)
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
        }

        // fallback: not authorized
        return Task.CompletedTask;
    }
}
