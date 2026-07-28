using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Hosts;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Hosts;

public class HostDashboardService : IHostDashboardService
{
    private readonly GoIslandDbContext _context;
    private readonly IConfiguration _configuration;

    public HostDashboardService(GoIslandDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<HostDashboardResponse?> GetAsync(int hostUserId)
    {
        var approved = await _context.HostProfiles.AsNoTracking().AnyAsync(profile =>
            profile.UserId == hostUserId
            && profile.VerificationStatus == HostVerificationStatuses.Approved);
        if (!approved)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var experiences = _context.Experiences.AsNoTracking()
            .Where(experience => experience.HostId == hostUserId);
        var upcomingSchedules = from schedule in _context.ExperienceSchedules.AsNoTracking()
                                join experience in experiences on schedule.ExperienceId equals experience.Id
                                where schedule.StartsAt > now && schedule.Status == ScheduleStatuses.Scheduled
                                select new { Schedule = schedule, Experience = experience };
        var upcomingReservations = from reservation in _context.Reservations.AsNoTracking()
                                   join schedule in _context.ExperienceSchedules.AsNoTracking()
                                       on reservation.ScheduleId equals schedule.Id
                                   join experience in experiences on reservation.ExperienceId equals experience.Id
                                   where schedule.StartsAt > now
                                       && reservation.Status == ReservationStatuses.Confirmed
                                   select reservation;
        var hostPayments = from payment in _context.Payments.AsNoTracking()
                           join reservation in _context.Reservations.AsNoTracking()
                               on payment.ReservationId equals reservation.Id
                           join experience in experiences on reservation.ExperienceId equals experience.Id
                           where payment.Status == PaymentStatuses.Paid
                           select payment;
        var visibleReviews = _context.Reviews.AsNoTracking().Where(review =>
            review.HostId == hostUserId
            && review.ModerationStatus == ReviewModerationStatuses.Visible);

        var rating = await visibleReviews
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Average = (decimal?)group.Average(review => review.Rating),
                Count = group.Count()
            })
            .SingleOrDefaultAsync();

        return new HostDashboardResponse
        {
            TotalExperiences = await experiences.CountAsync(),
            PublishedExperiences = await experiences.CountAsync(experience =>
                experience.ApprovalStatus == ExperienceApprovalStatuses.Approved),
            UpcomingSchedules = await upcomingSchedules.CountAsync(),
            UpcomingReservations = await upcomingReservations.CountAsync(),
            ReservedSpots = await upcomingReservations.SumAsync(reservation => (int?)reservation.Quantity) ?? 0,
            CompletedReservations = await _context.Reservations.AsNoTracking()
                .Where(reservation => reservation.Status == ReservationStatuses.Completed)
                .Join(experiences, reservation => reservation.ExperienceId, experience => experience.Id,
                    (reservation, _) => reservation)
                .CountAsync(),
            NetEarnings = await hostPayments.SumAsync(payment => (decimal?)payment.HostNetAmount) ?? 0m,
            Currency = _configuration["Payments:Currency"] ?? "USD",
            AverageRating = rating?.Average is null ? null : Math.Round(rating.Average.Value, 1),
            ReviewCount = rating?.Count ?? 0,
            NextSchedules = await upcomingSchedules
                .OrderBy(item => item.Schedule.StartsAt)
                .Take(5)
                .Select(item => new HostDashboardScheduleResponse
                {
                    Id = item.Schedule.Id,
                    ExperienceId = item.Experience.Id,
                    ExperienceTitle = item.Experience.Title,
                    StartsAt = item.Schedule.StartsAt,
                    EndsAt = item.Schedule.EndsAt,
                    ReservedSpots = item.Schedule.Capacity - item.Schedule.AvailableSpots,
                    Capacity = item.Schedule.Capacity
                })
                .ToArrayAsync()
        };
    }
}
