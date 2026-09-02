using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Entities;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ElixomClaim.Lib.Tests.Authorization;

public class AuthorizationTests
{
    [Theory]
    [InlineData(UserRole.Blocked, UserRole.User, false)]
    [InlineData(UserRole.User, UserRole.User, true)]
    [InlineData(UserRole.User, UserRole.Teller, false)]
    [InlineData(UserRole.Teller, UserRole.User, true)]
    [InlineData(UserRole.Teller, UserRole.Teller, true)]
    [InlineData(UserRole.Teller, UserRole.Manager, false)]
    [InlineData(UserRole.Manager, UserRole.Teller, true)]
    [InlineData(UserRole.Manager, UserRole.Manager, true)]
    [InlineData(UserRole.Manager, UserRole.Accountant, false)]
    [InlineData(UserRole.Accountant, UserRole.Manager, true)]
    [InlineData(UserRole.Accountant, UserRole.Accountant, true)]
    [InlineData(UserRole.Accountant, UserRole.Administrator, false)]
    [InlineData(UserRole.Administrator, UserRole.Accountant, true)]
    [InlineData(UserRole.Administrator, UserRole.Administrator, true)]
    public void HasMinimumRole_EvaluatesHierarchyAndBlockedState(UserRole currentRole, UserRole requiredRole, bool expected)
    {
        var result = currentRole.HasMinimumRole(requiredRole);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task MinimumRoleHandler_Succeeds_WhenRoleMeetsRequirement()
    {
        var handler = new MinimumRoleHandler();
        var requirement = new MinimumRoleRequirement(UserRole.Teller);
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, UserRole.Manager.ToString())
        ]));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task MinimumRoleHandler_Fails_WhenUserIsBlockedOrInsufficient()
    {
        var handler = new MinimumRoleHandler();
        var requirement = new MinimumRoleRequirement(UserRole.User);
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, UserRole.Blocked.ToString())
        ]));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private class SampleResource : IOwnableResource
    {
        public string OwnerUserId { get; set; } = string.Empty;
    }

    [Fact]
    public async Task ResourceOwnershipHandler_Succeeds_WhenUserIsResourceOwner()
    {
        var handler = new ResourceOwnershipHandler();
        var requirement = new ResourceOwnershipRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, UserRole.User.ToString())
        ]));
        var resource = new SampleResource { OwnerUserId = "user-123" };
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ResourceOwnershipHandler_Fails_WhenUserIsNotResourceOwner()
    {
        var handler = new ResourceOwnershipHandler();
        var requirement = new ResourceOwnershipRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, UserRole.User.ToString())
        ]));
        var resource = new SampleResource { OwnerUserId = "user-999" };
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ResourceOwnershipHandler_Succeeds_ForAdminOrManager_EvenIfNotOwner()
    {
        var handler = new ResourceOwnershipHandler();
        var requirement = new ResourceOwnershipRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, UserRole.Administrator.ToString())
        ]));
        var resource = new SampleResource { OwnerUserId = "user-999" };
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ResourceOwnershipHandler_Fails_WhenUserIsBlocked_EvenIfOwner()
    {
        var handler = new ResourceOwnershipHandler();
        var requirement = new ResourceOwnershipRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, UserRole.Blocked.ToString())
        ]));
        var resource = new SampleResource { OwnerUserId = "user-123" };
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
