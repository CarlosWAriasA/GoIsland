using GoIsland.Api.Data;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Repositories;

public class EfPaymentRepository : IPaymentRepository
{
    private readonly GoIslandDbContext _context;

    public EfPaymentRepository(GoIslandDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Payment>> GetByReservationIdAsync(int reservationId)
    {
        return await _context.Payments
            .Where(payment => payment.ReservationId == reservationId)
            .ToListAsync();
    }

    public Task<Payment?> GetByIdAsync(int id)
    {
        return _context.Payments.FindAsync(id).AsTask();
    }

    public async Task<Payment> AddAsync(Payment payment)
    {
        await _context.Payments.AddAsync(payment);
        return payment;
    }

    public Task UpdateAsync(Payment payment)
    {
        _context.Payments.Update(payment);
        return Task.CompletedTask;
    }
}
