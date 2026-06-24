using Microsoft.AspNetCore.Identity;
using Vcs.Domain.Enums;

namespace Vcs.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public AuditActionType Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ApplicationUser? User { get; set; }
}