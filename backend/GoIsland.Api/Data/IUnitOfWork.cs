using GoIsland.Api.Repositories;

namespace GoIsland.Api.Data;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IExperienceRepository Experiences { get; }
    IReservationRepository Reservations { get; }
    IPaymentRepository Payments { get; }
    IPasswordResetTokenRepository PasswordResetTokens { get; }
    Task<int> CommitAsync();
}
