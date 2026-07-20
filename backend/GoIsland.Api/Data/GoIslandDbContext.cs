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
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

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

        modelBuilder.Entity<Experience>(entity =>
        {
            entity.ToTable("experiences");
            entity.HasKey(experience => experience.Id);
            entity.Property(experience => experience.Id).HasColumnName("id");
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
            entity.Property(experience => experience.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("reservations");
            entity.HasKey(reservation => reservation.Id);
            entity.Property(reservation => reservation.Id).HasColumnName("id");
            entity.Property(reservation => reservation.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(reservation => reservation.ExperienceId).HasColumnName("experience_id").IsRequired();
            entity.Property(reservation => reservation.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(reservation => reservation.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
            entity.Property(reservation => reservation.TotalAmount).HasColumnName("total_amount").HasPrecision(10, 2);
            entity.Property(reservation => reservation.ReservationDate).HasColumnName("reservation_date").IsRequired();
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
    }
}
