namespace GoIsland.Api.Models;

public static class UserRoles
{
    public const string Tourist = "Tourist";
    public const string Host = "Host";
    public const string Admin = "Admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Tourist,
        Host,
        Admin
    };

    public static readonly IReadOnlySet<string> PublicRegistration = new HashSet<string>
    {
        Tourist
    };
}
