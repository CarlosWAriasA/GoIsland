namespace GoIsland.Api.Models;

public static class HostVerificationStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Suspended = "Suspended";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Pending,
        Approved,
        Rejected,
        Suspended
    };
}
