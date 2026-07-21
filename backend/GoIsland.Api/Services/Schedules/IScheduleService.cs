using GoIsland.Api.DTOs.Schedules;

namespace GoIsland.Api.Services.Schedules;

public enum ScheduleOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidDates,
    InvalidStatus,
    CapacityConflict,
    HasReservations
}

public record ScheduleOperationResult(
    ScheduleOperationStatus Status,
    ScheduleResponse? Schedule = null);

public interface IScheduleService
{
    Task<ScheduleOperationResult> CreateAsync(int hostUserId, int experienceId, CreateScheduleRequest request);
    Task<IReadOnlyCollection<ScheduleResponse>?> GetForHostAsync(int hostUserId, int experienceId);
    Task<ScheduleOperationResult> UpdateAsync(int hostUserId, int id, UpdateScheduleRequest request);
    Task<ScheduleOperationResult> DeleteAsync(int hostUserId, int id);
    Task<IReadOnlyCollection<ScheduleResponse>?> GetAvailabilityAsync(int experienceId, DateTime? from, DateTime? to, int quantity);
}
