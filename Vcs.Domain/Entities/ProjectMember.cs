using Microsoft.AspNetCore.Identity;
using Vcs.Domain.Enums;

namespace Vcs.Domain.Entities;

public sealed class ProjectMember
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public ProjectRole Role { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public Project Project { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}