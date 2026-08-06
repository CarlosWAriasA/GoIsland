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
    HasReservations,
    ConcurrencyConflict
}

public record ScheduleOperationResult(
    ScheduleOperationStatus Status,
    ScheduleResponse? Schedule = null);

public record RecurringScheduleOperationResult(
    ScheduleOperationStatus Status,
    RecurringSchedulePreviewResponse? Preview = null,
    RecurringScheduleGenerationResponse? Generation = null);

public record ScheduleBatchOperationResult(
    ScheduleOperationStatus Status,
    ScheduleBatchResponse? Batch = null);

public interface IScheduleService
{
    Task<ScheduleOperationResult> CreateAsync(int hostUserId, int experienceId, CreateScheduleRequest request);
    Task<RecurringScheduleOperationResult> PreviewRecurringAsync(
        int hostUserId,
        int experienceId,
        RecurringScheduleRequest request);
    Task<RecurringScheduleOperationResult> GenerateRecurringAsync(
        int hostUserId,
        int experienceId,
        RecurringScheduleRequest request);
    Task<RecurringScheduleOperationResult> PreviewCopyWeekAsync(
        int hostUserId,
        int experienceId,
        CopyScheduleWeekRequest request);
    Task<RecurringScheduleOperationResult> CopyWeekAsync(
        int hostUserId,
        int experienceId,
        CopyScheduleWeekRequest request);
    Task<ScheduleBatchOperationResult> CloseBatchAsync(
        int hostUserId,
        int experienceId,
        ScheduleSelectionRequest request);
    Task<ScheduleBatchOperationResult> UpdateCapacityBatchAsync(
        int hostUserId,
        int experienceId,
        BulkCapacityRequest request);
    Task<IReadOnlyCollection<ScheduleResponse>?> GetForHostAsync(int hostUserId, int experienceId);
    Task<ScheduleOperationResult> UpdateAsync(int hostUserId, int id, UpdateScheduleRequest request);
    Task<ScheduleOperationResult> DeleteAsync(int hostUserId, int id);
    Task<IReadOnlyCollection<ScheduleResponse>?> GetAvailabilityAsync(int experienceId, DateTime? from, DateTime? to, int quantity);
}
