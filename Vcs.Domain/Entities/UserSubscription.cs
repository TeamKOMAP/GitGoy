using Microsoft.AspNetCore.Identity;
using Vcs.Domain.Enums;

namespace Vcs.Domain.Entities;

public sealed class UserSubscription
{
    public Guid Id { get; set; }
    public Guid FollowerId { get; set; }
    public Guid FollowingId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ApplicationUser Follower { get; set; } = null!;
    public ApplicationUser Following { get; set; } = null!;
}