using Microsoft.AspNetCore.Identity;
using Vcs.Domain.Enums;

namespace Vcs.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ApplicationUser User { get; set; } = null!;
}