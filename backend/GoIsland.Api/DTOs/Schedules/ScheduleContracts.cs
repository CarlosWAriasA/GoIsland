using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Schedules;

public class CreateScheduleRequest
{
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    [Range(1, 100000, ErrorMessage = "La capacidad debe ser mayor que cero.")]
    public int Capacity { get; set; }
}

public class UpdateScheduleRequest : CreateScheduleRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class ScheduleResponse
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int Capacity { get; set; }
    public int AvailableSpots { get; set; }
    public bool IsUnlimitedCapacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
