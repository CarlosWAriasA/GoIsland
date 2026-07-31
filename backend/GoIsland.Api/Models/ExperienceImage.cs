namespace GoIsland.Api.Models;

public class ExperienceImage
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Experience Experience { get; set; } = null!;
}
