using Microsoft.AspNetCore.Identity;
using Vcs.Domain.Enums;

namespace Vcs.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Навигационные свойства (связи)
    public ICollection<Project> OwnedProjects { get; set; } = [];
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];
    public ICollection<UserSubscription> Following { get; set; } = [];
    public ICollection<UserSubscription> Followers { get; set; } = [];
}