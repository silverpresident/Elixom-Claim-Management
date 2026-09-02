namespace ElixomClaim.Lib.Authorization;

public static class PolicyNames
{
    public const string RequireActiveUser = "RequireActiveUser";
    public const string RequireTeller = "RequireTeller";
    public const string RequireManager = "RequireManager";
    public const string RequireAccountant = "RequireAccountant";
    public const string RequireAdministrator = "RequireAdministrator";
    public const string RequireResourceOwnership = "RequireResourceOwnership";
}
