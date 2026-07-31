namespace GoIsland.Api.Models;

public static class ExperienceDifficulties
{
    public const string Easy = "Easy";
    public const string Moderate = "Moderate";
    public const string Demanding = "Demanding";
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Easy, Moderate, Demanding
    };
}

public static class CancellationPolicies
{
    public const string Flexible = "Flexible";
    public const string Moderate = "Moderate";
    public const string Strict = "Strict";
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Flexible, Moderate, Strict
    };
}

public class ExperienceItineraryItem
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public int SortOrder { get; set; }
    public Experience Experience { get; set; } = null!;
}
