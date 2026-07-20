using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Reservations;

public class ReservationService : IReservationService
{
    private const decimal MaxTotalAmount = 99_999_999.99m;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReservationService> _logger;
    private readonly List<IReservationObserver> _observers = [];

    public ReservationService(
        IUnitOfWork unitOfWork,
        IEnumerable<IReservationObserver> observers,
        ILogger<ReservationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        foreach (var observer in observers)
        {
            Subscribe(observer);
        }
    }

    public void Subscribe(IReservationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
    }

    public void Unsubscribe(IReservationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        _observers.Remove(observer);
    }

    public async Task NotifyAsync(ReservationEvent reservationEvent)
    {
        foreach (var observer in _observers.ToArray())
        {
            try
            {
                await observer.UpdateAsync(reservationEvent);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "El observador {ObserverName} fallo al procesar la reserva {ReservationId}.",
                    observer.GetType().Name,
                    reservationEvent.Reservation.Id);
            }
        }
    }

    public async Task<ReservationCreationResult> CreateAsync(int userId, CreateReservationRequest request)
    {
        var experience = await _unitOfWork.Experiences.GetForReservationAsync(request.ExperienceId);
        if (experience is null)
        {
            return new ReservationCreationResult(ReservationCreationStatus.ExperienceNotFound);
        }

        if (experience.AvailableSpots < request.Quantity)
        {
            return new ReservationCreationResult(ReservationCreationStatus.InsufficientSpots);
        }

        if (experience.Price > MaxTotalAmount / request.Quantity)
        {
            return new ReservationCreationResult(ReservationCreationStatus.AmountOutOfRange);
        }

        experience.AvailableSpots -= request.Quantity;

        var reservation = new Reservation
        {
            UserId = userId,
            ExperienceId = experience.Id,
            Quantity = request.Quantity,
            Status = "Pending",
            TotalAmount = experience.Price * request.Quantity
        };

        await _unitOfWork.Experiences.UpdateAsync(experience);
        await _unitOfWork.Reservations.AddAsync(reservation);

        try
        {
            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ReservationCreationResult(ReservationCreationStatus.ConcurrencyConflict);
        }

        var response = ToResponse(reservation);
        await NotifyAsync(new ReservationEvent(
            ReservationEventType.Created,
            response,
            experience.AvailableSpots,
            DateTime.UtcNow));

        return new ReservationCreationResult(ReservationCreationStatus.Success, response);
    }

    public async Task<IReadOnlyCollection<ReservationResponse>> GetByUserIdAsync(int userId)
    {
        var reservations = await _unitOfWork.Reservations.GetByUserIdAsync(userId);
        return reservations.Select(ToResponse).ToArray();
    }

    public async Task<ReservationResponse?> GetByIdAsync(int id, int userId, bool isAdmin)
    {
        var reservation = await _unitOfWork.Reservations.GetByIdAsync(id);
        if (reservation is null || (!isAdmin && reservation.UserId != userId))
        {
            return null;
        }

        return ToResponse(reservation);
    }

    private static ReservationResponse ToResponse(Reservation reservation)
    {
        return new ReservationResponse
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            ExperienceId = reservation.ExperienceId,
            Quantity = reservation.Quantity,
            Status = reservation.Status,
            TotalAmount = reservation.TotalAmount,
            ReservationDate = reservation.ReservationDate
        };
    }
}
