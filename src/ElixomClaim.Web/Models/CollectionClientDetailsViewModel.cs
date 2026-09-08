using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Web.Models;

public sealed record CollectionClientDetailsViewModel(
    CollectionClient Client,
    IReadOnlyList<CollectionClientAssignableUser> AvailableUsers);

public sealed record CollectionClientAssignableUser(Guid Id, string Name, string Email);
