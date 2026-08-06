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

public class RecurringScheduleRequest : IValidatableObject
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }

    [MinLength(1, ErrorMessage = "Selecciona al menos un día de la semana.")]
    [MaxLength(7, ErrorMessage = "Selecciona como máximo los siete días de la semana.")]
    public int[] Weekdays { get; set; } = [];

    [Range(1, 100000, ErrorMessage = "La capacidad debe ser mayor que cero.")]
    public int Capacity { get; set; }

    [MaxLength(366, ErrorMessage = "Hay demasiadas fechas excluidas.")]
    public DateOnly[] ExcludedDates { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "La fecha final debe ser igual o posterior a la inicial.",
                [nameof(StartDate), nameof(EndDate)]);
        }

        if (EndDate.DayNumber - StartDate.DayNumber > 366)
        {
            yield return new ValidationResult(
                "El rango no puede superar 12 meses.",
                [nameof(StartDate), nameof(EndDate)]);
        }

        if (Weekdays.Any(day => day is < 0 or > 6))
        {
            yield return new ValidationResult(
                "Los días seleccionados no son válidos.",
                [nameof(Weekdays)]);
        }

        if (EndsAt <= StartsAt)
        {
            yield return new ValidationResult(
                "La hora final debe ser posterior a la inicial.",
                [nameof(StartsAt), nameof(EndsAt)]);
        }

        if (ExcludedDates.Any(date => date < StartDate || date > EndDate))
        {
            yield return new ValidationResult(
                "Las fechas excluidas deben estar dentro del rango.",
                [nameof(ExcludedDates)]);
        }
    }
}

public static class RecurringScheduleDispositions
{
    public const string WillCreate = "WillCreate";
    public const string Existing = "Existing";
    public const string Excluded = "Excluded";
}

public class RecurringSchedulePreviewItem
{
    public DateOnly LocalDate { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Disposition { get; set; } = string.Empty;
}

public class RecurringSchedulePreviewResponse
{
    public string TimeZoneId { get; set; } = string.Empty;
    public IReadOnlyCollection<RecurringSchedulePreviewItem> Items { get; set; } = [];
    public int ToCreate { get; set; }
    public int Existing { get; set; }
    public int Excluded { get; set; }
}

public class RecurringScheduleGenerationResponse
{
    public int Created { get; set; }
    public int Existing { get; set; }
    public int Excluded { get; set; }
    public IReadOnlyCollection<ScheduleResponse> Schedules { get; set; } = [];
}

public class CopyScheduleWeekRequest : IValidatableObject
{
    public DateOnly SourceWeekStart { get; set; }
    public DateOnly TargetWeekStart { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SourceWeekStart.DayOfWeek != DayOfWeek.Monday)
        {
            yield return new ValidationResult(
                "La semana de origen debe comenzar un lunes.",
                [nameof(SourceWeekStart)]);
        }

        if (TargetWeekStart.DayOfWeek != DayOfWeek.Monday)
        {
            yield return new ValidationResult(
                "La semana de destino debe comenzar un lunes.",
                [nameof(TargetWeekStart)]);
        }

        if (SourceWeekStart == TargetWeekStart)
        {
            yield return new ValidationResult(
                "Selecciona una semana de destino diferente.",
                [nameof(TargetWeekStart)]);
        }
    }
}

public class ScheduleSelectionRequest : IValidatableObject
{
    [MinLength(1, ErrorMessage = "Selecciona al menos un horario.")]
    [MaxLength(200, ErrorMessage = "Puedes modificar hasta 200 horarios por operación.")]
    public int[] ScheduleIds { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScheduleIds.Any(id => id <= 0))
        {
            yield return new ValidationResult(
                "La selección contiene un horario no válido.",
                [nameof(ScheduleIds)]);
        }
    }
}

public class BulkCapacityRequest : ScheduleSelectionRequest
{
    [Range(1, 100000, ErrorMessage = "La capacidad debe ser mayor que cero.")]
    public int Capacity { get; set; }
}

public class ScheduleBatchResponse
{
    public IReadOnlyCollection<ScheduleResponse> Schedules { get; set; } = [];
    public IReadOnlyCollection<int> ConflictingScheduleIds { get; set; } = [];
}
