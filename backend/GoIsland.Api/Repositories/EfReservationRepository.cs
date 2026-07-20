using GoIsland.Api.Data;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Repositories;

public class EfReservationRepository : IReservationRepository
{
    private readonly GoIslandDbContext _context;

    public EfReservationRepository(GoIslandDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Reservation>> GetByUserIdAsync(int userId)
    {
        return await _context.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.UserId == userId)
            .OrderByDescending(reservation => reservation.ReservationDate)
            .ToListAsync();
    }

    public Task<Reservation?> GetByIdAsync(int id)
    {
        return _context.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(reservation => reservation.Id == id);
    }

    public async Task<Reservation> AddAsync(Reservation reservation)
    {
        await _context.Reservations.AddAsync(reservation);
        return reservation;
    }

    public Task UpdateAsync(Reservation reservation)
    {
        _context.Reservations.Update(reservation);
        return Task.CompletedTask;
    }
}
