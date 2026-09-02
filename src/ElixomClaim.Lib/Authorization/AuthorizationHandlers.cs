using System.Security.Claims;
using ElixomClaim.Lib.Entities;
using Microsoft.AspNetCore.Authorization;

namespace ElixomClaim.Lib.Authorization;

public class MinimumRoleRequirement : IAuthorizationRequirement
{
    public UserRole MinimumRole { get; }

    public MinimumRoleRequirement(UserRole minimumRole)
    {
        MinimumRole = minimumRole;
    }
}

public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MinimumRoleRequirement requirement)
    {
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if (Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var userRole))
        {
            if (userRole.HasMinimumRole(requirement.MinimumRole))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}

public class ResourceOwnershipRequirement : IAuthorizationRequirement
{
}

public interface IOwnableResource
{
    string OwnerUserId { get; }
}

public class ResourceOwnershipHandler : AuthorizationHandler<ResourceOwnershipRequirement, IOwnableResource>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ResourceOwnershipRequirement requirement, IOwnableResource resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if (Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var userRole) && userRole == UserRole.Blocked)
        {
            return Task.CompletedTask;
        }

        // Admins and Managers bypass ownership constraints for management operations
        if (userRole == UserRole.Administrator || userRole == UserRole.Manager)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (!string.IsNullOrEmpty(userId) && string.Equals(userId, resource.OwnerUserId, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
