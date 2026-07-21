namespace GoIsland.Api.Models;

public static class ExperienceApprovalStatuses
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Suspended = "Suspended";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft,
        PendingReview,
        Approved,
        Rejected,
        Suspended
    };
}
