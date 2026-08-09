namespace GoIsland.Api.Services.Reservations;

public class ReservationExpirationOptions
{
    public const string SectionName = "Reservations:Expiration";

    public int HoldMinutes { get; set; } = 15;
    public int BookingCutoffMinutes { get; set; } = 30;
    public int CompletionGraceMinutes { get; set; } = 120;
    public int PollIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;

    public TimeSpan HoldDuration => TimeSpan.FromMinutes(HoldMinutes);
    public TimeSpan BookingCutoff => TimeSpan.FromMinutes(BookingCutoffMinutes);
    public TimeSpan CompletionGrace => TimeSpan.FromMinutes(CompletionGraceMinutes);
    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
}
