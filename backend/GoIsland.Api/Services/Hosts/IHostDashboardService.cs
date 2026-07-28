using GoIsland.Api.DTOs.Hosts;

namespace GoIsland.Api.Services.Hosts;

public interface IHostDashboardService
{
    Task<HostDashboardResponse?> GetAsync(int hostUserId);
}
