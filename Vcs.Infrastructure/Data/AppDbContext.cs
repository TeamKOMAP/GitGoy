using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vcs.Domain.Entities;

namespace Vcs.Infrastructure.Data;

public sealed class AppDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Уникальность подписки (пользователь не может подписаться дважды)
        builder.Entity<UserSubscription>()
            .HasIndex(x => new { x.FollowerId, x.FollowingId })
            .IsUnique();

        // Связи для подписок
        builder.Entity<UserSubscription>()
            .HasOne(x => x.Follower)
            .WithMany(x => x.Following)
            .HasForeignKey(x => x.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserSubscription>()
            .HasOne(x => x.Following)
            .WithMany(x => x.Followers)
            .HasForeignKey(x => x.FollowingId)
            .OnDelete(DeleteBehavior.Restrict);

        // Уникальность проекта (у одного пользователя не может быть двух проектов с одинаковым именем)
        builder.Entity<Project>()
            .HasIndex(x => new { x.OwnerId, x.Name })
            .IsUnique();

        // Уникальность участника (пользователь не может быть добавлен в проект дважды)
        builder.Entity<ProjectMember>()
            .HasIndex(x => new { x.ProjectId, x.UserId })
            .IsUnique();

        builder.Entity<ProjectMember>()
            .HasOne(x => x.Project)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<ProjectMember>()
            .HasOne(x => x.User)
            .WithMany(x => x.ProjectMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Уникальность RefreshToken (по хэшу)
        builder.Entity<RefreshToken>()
            .HasIndex(x => x.TokenHash)
            .IsUnique();
    }
}