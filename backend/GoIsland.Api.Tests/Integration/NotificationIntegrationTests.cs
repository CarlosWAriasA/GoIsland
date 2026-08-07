using GoIsland.Api.DTOs.Notifications;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Notifications;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

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
            new RegisterDeviceRequest
            {
                Endpoint = $"https://push.goisland.test/{Guid.NewGuid():N}",
                P256dh = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "public-key",
                Auth = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            });
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

        var unreadPage = await service.GetAsync(first.Id, new NotificationListRequest
        {
            UnreadOnly = true,
            PageSize = 1
        });
        Assert.Empty(unreadPage.Items);
        Assert.Equal(0, unreadPage.TotalItems);
    }

    [Fact]
    public async Task Enqueue_WithDeliverAtInFuture_IsNotProcessedUntilTime()
    {
        var user = await SeedUserAsync();
        var outboxWriter = GetRequiredService<IOutboxWriter>();
        var deliverAt = DateTime.UtcNow.AddHours(2);

        await outboxWriter.EnqueueAsync(user.Id, "VisitReminder", "Recordatorio futuro", "Mensaje diferido", deliverAt: deliverAt);
        await Context.SaveChangesAsync();

        var processor = GetRequiredService<OutboxProcessor>();
        var processedCount = await processor.ProcessPendingAsync();
        Assert.Equal(0, processedCount);

        var msg = await Context.OutboxMessages.SingleAsync(m => m.UserId == user.Id && m.Type == "VisitReminder");
        Assert.Equal(OutboxStatuses.Pending, msg.Status);
        Assert.True(msg.NextAttemptAt > DateTime.UtcNow);

        // Manually move deliverAt to past to simulate time passing
        msg.NextAttemptAt = DateTime.UtcNow.AddMinutes(-5);
        await Context.SaveChangesAsync();

        processedCount = await processor.ProcessPendingAsync();
        Assert.Equal(1, processedCount);

        var deliveredNotification = await Context.Notifications.SingleAsync(n => n.UserId == user.Id);
        Assert.Equal("Recordatorio futuro", deliveredNotification.Title);
        Assert.True(deliveredNotification.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
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
