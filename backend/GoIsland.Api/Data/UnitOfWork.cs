using GoIsland.Api.Repositories;

namespace GoIsland.Api.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly GoIslandDbContext _context;

    public UnitOfWork(
        GoIslandDbContext context,
        IUserRepository users,
        IExperienceRepository experiences,
        IReservationRepository reservations,
        IPaymentRepository payments,
        IPasswordResetTokenRepository passwordResetTokens)
    {
        _context = context;
        Users = users;
        Experiences = experiences;
        Reservations = reservations;
        Payments = payments;
        PasswordResetTokens = passwordResetTokens;
    }

    public IUserRepository Users { get; }
    public IExperienceRepository Experiences { get; }
    public IReservationRepository Reservations { get; }
    public IPaymentRepository Payments { get; }
    public IPasswordResetTokenRepository PasswordResetTokens { get; }

    public Task<int> CommitAsync()
    {
        return _context.SaveChangesAsync();
    }
}
