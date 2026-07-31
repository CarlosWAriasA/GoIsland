namespace GoIsland.Api.Models;

public static class ExperienceCategories
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [
            "Acuático",
            "Aventura",
            "Arte y cultura",
            "Bienestar",
            "Cruceros",
            "Deportes",
            "Gastronomía",
            "Historia",
            "Naturaleza",
            "Nocturna",
            "Talleres"
        ],
        StringComparer.OrdinalIgnoreCase);
}

public static class ExperienceCapacity
{
    public const int UnlimitedValue = 1_000_000;
}
