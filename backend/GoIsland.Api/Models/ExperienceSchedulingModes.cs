namespace GoIsland.Api.Models;

public static class ExperienceSchedulingModes
{
    public const string HostScheduled = "HostScheduled";
    public const string SelfGuided = "SelfGuided";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        HostScheduled,
        SelfGuided
    };
}
