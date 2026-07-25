using GoIsland.Api.DTOs.Notifications;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using GoIsland.Api.Tests.Infrastructure;

namespace GoIsland.Api.Tests.Integration;

public class NotificationIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task PreferencesDevicesAndReadState_AreScopedToOwner()
    {
        var first = await SeedUserAsync();
        var second = await SeedUserAsync();
        var service = GetRequiredService<INotificationService>();
        var preferences = await service.UpdatePreferencesAsync(first.Id, new UpdateNotificationPreferenceRequest
        {
            DashboardEnabled = true, EmailEnabled = false, PushEnabled = true
        });
        var device = await service.RegisterDeviceAsync(first.Id,
            new RegisterDeviceRequest { Platform = "Web", Token = $"token-{Guid.NewGuid():N}" });
        var outbox = new OutboxMessage
        {
            UserId = first.Id, Type = "Test", Title = "Evento persistido", Message = "Mensaje de integracion"
        };
        Context.OutboxMessages.Add(outbox);
        await Context.SaveChangesAsync();
        var notification = new Notification
        {
            UserId = first.Id, OutboxMessageId = outbox.Id, Type = outbox.Type,
            Title = outbox.Title, Message = outbox.Message
        };
        Context.Notifications.Add(notification);
        await Context.SaveChangesAsync();

        var forbiddenRead = await service.MarkReadAsync(second.Id, notification.Id);
        var read = await service.MarkReadAsync(first.Id, notification.Id);
        var forbiddenDelete = await service.DeleteDeviceAsync(second.Id, device.Id);
        var deleted = await service.DeleteDeviceAsync(first.Id, device.Id);

        Assert.False(preferences.EmailEnabled);
        Assert.Null(forbiddenRead);
        Assert.NotNull(read!.ReadAt);
        Assert.False(forbiddenDelete);
        Assert.True(deleted);
    }

    private async Task<User> SeedUserAsync()
    {
        var user = new User
        {
            FullName = "Usuario notificaciones", Email = $"{Guid.NewGuid():N}@goisland.test",
            PasswordHash = "hash-integracion", Role = UserRoles.Tourist
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }
}
