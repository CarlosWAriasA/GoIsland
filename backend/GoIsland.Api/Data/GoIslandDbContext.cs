using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Data;

public class GoIslandDbContext : DbContext
{
    public GoIslandDbContext(DbContextOptions<GoIslandDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ExperienceSchedule> ExperienceSchedules => Set<ExperienceSchedule>();
    public DbSet<ReservationStatusHistory> ReservationStatusHistories => Set<ReservationStatusHistory>();
    public DbSet<ReservationIdempotencyKey> ReservationIdempotencyKeys => Set<ReservationIdempotencyKey>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<HostProfile> HostProfiles => Set<HostProfile>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(user => user.Role).HasColumnName("role").HasMaxLength(40).IsRequired();
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<UserExternalLogin>(entity =>
        {
            entity.ToTable("user_external_logins");
            entity.HasKey(login => login.Id);
            entity.Property(login => login.Id).HasColumnName("id");
            entity.Property(login => login.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(login => login.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
            entity.Property(login => login.ProviderSubject).HasColumnName("provider_subject").HasMaxLength(255).IsRequired();
            entity.Property(login => login.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasIndex(login => new { login.Provider, login.ProviderSubject }).IsUnique();
            entity.HasIndex(login => new { login.UserId, login.Provider }).IsUnique();
            entity.HasOne(login => login.User)
                .WithMany()
                .HasForeignKey(login => login.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Experience>(entity =>
        {
            entity.ToTable("experiences");
            entity.HasKey(experience => experience.Id);
            entity.Property(experience => experience.Id).HasColumnName("id");
            entity.Property(experience => experience.HostId).HasColumnName("host_id").IsRequired();
            entity.Property(experience => experience.Title).HasColumnName("title").HasMaxLength(160).IsRequired();
            entity.Property(experience => experience.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
            entity.Property(experience => experience.Location).HasColumnName("location").HasMaxLength(160).IsRequired();
            entity.Property(experience => experience.Category).HasColumnName("category").HasMaxLength(80).IsRequired();
            entity.Property(experience => experience.Price).HasColumnName("price").HasPrecision(10, 2);
            entity.Property(experience => experience.Capacity).HasColumnName("capacity").IsRequired();
            entity.Property(experience => experience.AvailableSpots)
                .HasColumnName("available_spots")
                .IsRequired()
                .IsConcurrencyToken();
            entity.Property(experience => experience.IsApproved).HasColumnName("is_approved").IsRequired();
            entity.Property(experience => experience.ApprovalStatus).HasColumnName("approval_status").HasMaxLength(40).IsRequired();
            entity.Property(experience => experience.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
            entity.Property(experience => experience.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(experience => experience.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id");
            entity.Property(experience => experience.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(experience => experience.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(experience => experience.HostId);
            entity.HasIndex(experience => experience.ApprovalStatus);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("reservations");
            entity.HasKey(reservation => reservation.Id);
            entity.Property(reservation => reservation.Id).HasColumnName("id");
            entity.Property(reservation => reservation.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(reservation => reservation.ExperienceId).HasColumnName("experience_id").IsRequired();
            entity.Property(reservation => reservation.ScheduleId).HasColumnName("schedule_id").IsRequired();
            entity.Property(reservation => reservation.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(reservation => reservation.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
            entity.Property(reservation => reservation.TotalAmount).HasColumnName("total_amount").HasPrecision(10, 2);
            entity.Property(reservation => reservation.ReservationDate).HasColumnName("reservation_date").IsRequired();
            entity.Property(reservation => reservation.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(reservation => reservation.CancelledAt).HasColumnName("cancelled_at");
            entity.HasIndex(reservation => reservation.ScheduleId);
        });

        modelBuilder.Entity<ExperienceSchedule>(entity =>
        {
            entity.ToTable("experience_schedules");
            entity.HasKey(schedule => schedule.Id);
            entity.Property(schedule => schedule.Id).HasColumnName("id");
            entity.Property(schedule => schedule.ExperienceId).HasColumnName("experience_id").IsRequired();
            entity.Property(schedule => schedule.StartsAt).HasColumnName("starts_at").IsRequired();
            entity.Property(schedule => schedule.EndsAt).HasColumnName("ends_at").IsRequired();
            entity.Property(schedule => schedule.Capacity).HasColumnName("capacity").IsRequired();
            entity.Property(schedule => schedule.AvailableSpots)
                .HasColumnName("available_spots")
                .IsRequired()
                .IsConcurrencyToken();
            entity.Property(schedule => schedule.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
            entity.Property(schedule => schedule.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(schedule => schedule.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(schedule => new { schedule.ExperienceId, schedule.StartsAt });
        });

        modelBuilder.Entity<ReservationStatusHistory>(entity =>
        {
            entity.ToTable("reservation_status_history");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).HasColumnName("id");
            entity.Property(history => history.ReservationId).HasColumnName("reservation_id").IsRequired();
            entity.Property(history => history.FromStatus).HasColumnName("from_status").HasMaxLength(40);
            entity.Property(history => history.ToStatus).HasColumnName("to_status").HasMaxLength(40).IsRequired();
            entity.Property(history => history.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
            entity.Property(history => history.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(history => history.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasIndex(history => new { history.ReservationId, history.CreatedAt });
            entity.HasOne(history => history.Reservation)
                .WithMany()
                .HasForeignKey(history => history.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReservationIdempotencyKey>(entity =>
        {
            entity.ToTable("reservation_idempotency_keys");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(item => item.Operation).HasColumnName("operation").HasMaxLength(80).IsRequired();
            entity.Property(item => item.Key).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64).IsRequired();
            entity.Property(item => item.ReservationId).HasColumnName("reservation_id").IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasIndex(item => new { item.UserId, item.Operation, item.Key }).IsUnique();
            entity.HasOne(item => item.Reservation)
                .WithMany()
                .HasForeignKey(item => item.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Id).HasColumnName("id");
            entity.Property(payment => payment.ReservationId).HasColumnName("reservation_id").IsRequired();
            entity.Property(payment => payment.Amount).HasColumnName("amount").HasPrecision(10, 2);
            entity.Property(payment => payment.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
            entity.Property(payment => payment.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.Id).HasColumnName("id");
            entity.Property(token => token.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(token => token.ExpiresAt).HasColumnName("expires_at").IsRequired();
            entity.Property(token => token.UsedAt).HasColumnName("used_at");
            entity.Property(token => token.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => token.UserId);
        });

        modelBuilder.Entity<HostProfile>(entity =>
        {
            entity.ToTable("host_profiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Id).HasColumnName("id");
            entity.Property(profile => profile.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(profile => profile.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
            entity.Property(profile => profile.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30).IsRequired();
            entity.Property(profile => profile.VerificationStatus).HasColumnName("verification_status").HasMaxLength(40).IsRequired();
            entity.Property(profile => profile.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
            entity.Property(profile => profile.SubmittedAt).HasColumnName("submitted_at").IsRequired();
            entity.Property(profile => profile.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(profile => profile.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id");
            entity.HasIndex(profile => profile.UserId).IsUnique();
            entity.HasIndex(profile => profile.VerificationStatus);
        });

        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.ToTable("admin_audit_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Id).HasColumnName("id");
            entity.Property(log => log.AdminUserId).HasColumnName("admin_user_id").IsRequired();
            entity.Property(log => log.EntityType).HasColumnName("entity_type").HasMaxLength(80).IsRequired();
            entity.Property(log => log.EntityId).HasColumnName("entity_id").IsRequired();
            entity.Property(log => log.Action).HasColumnName("action").HasMaxLength(80).IsRequired();
            entity.Property(log => log.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(log => log.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.HasIndex(log => new { log.EntityType, log.EntityId });
        });
    }
}
