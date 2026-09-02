namespace ElixomClaim.Lib.Entities;

public static class UserRoleExtensions
{
    public static bool HasMinimumRole(this UserRole currentRole, UserRole requiredRole)
    {
        if (currentRole == UserRole.Blocked)
        {
            return false;
        }

        return (int)currentRole >= (int)requiredRole;
    }
}
