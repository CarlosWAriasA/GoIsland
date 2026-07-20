using GoIsland.Api.Models;

namespace GoIsland.Api.Repositories;

public interface IUserExternalLoginRepository
{
    Task<UserExternalLogin?> GetByProviderSubjectAsync(string provider, string providerSubject);
    Task<UserExternalLogin?> GetByUserAndProviderAsync(int userId, string provider);
    Task<UserExternalLogin> AddAsync(UserExternalLogin externalLogin);
}
