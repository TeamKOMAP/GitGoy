using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vcs.Domain.Entities;
using Vcs.Domain.Enums;
using Vcs.Infrastructure.Data;

namespace Vcs.Infrastructure.Services;

public class ProjectService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly GitService _git;

    public ProjectService(AppDbContext db, UserManager<ApplicationUser> userManager, GitService git)
    {
        _db = db;
        _userManager = userManager;
        _git = git;
    }

    public async Task<Guid> GetOrCreateUserIdAsync(string? username)
    {
        var normalizedUsername = string.IsNullOrWhiteSpace(username)
            ? "desktop-client"
            : username.Trim();

        var user = await _userManager.FindByNameAsync(normalizedUsername);
        if (user is not null)
        {
            return user.Id;
        }

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedUsername,
            Email = $"{normalizedUsername}@example.local",
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    public async Task<ProjectRes> CreateProjectAsync(Guid ownerId, CreateProjectReq dto)
    {
        var visibility = dto.Visibility?.ToLower() == "private"
            ? ProjectVisibility.Private
            : ProjectVisibility.Public;

        var projectId = Guid.NewGuid();
        var project = new Project
        {
            Id = projectId,
            OwnerId = ownerId,
            Name = dto.Name,
            Description = dto.Description,
            Visibility = visibility,
            RepositoryPath = _git.GetRepositoryPath(projectId),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        _db.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = ownerId,
            Role = ProjectRole.Owner,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await _git.InitRepositoryAsync(project.Id);
        return ToDto(project);
    }

    public async Task<ProjectRes?> GetProjectAsync(Guid projectId, Guid? userId)
    {
        var project = await _db.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return null;
        if (!await HasReadAccessAsync(project, userId)) return null;
        await _git.InitRepositoryAsync(project.Id);
        return ToDto(project);
    }

    public async Task<List<ProjectRes>> GetMyProjectsAsync(Guid userId)
    {
        var projects = await _db.Projects.Include(p => p.Owner)
            .Where(p => p.OwnerId == userId)
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
        foreach (var project in projects)
        {
            await _git.InitRepositoryAsync(project.Id);
        }

        return projects.Select(ToDto).ToList();
    }

    public async Task<List<ProjectRes>> GetPublicProjectsAsync()
    {
        var projects = await _db.Projects.Include(p => p.Owner)
            .Where(p => p.Visibility == ProjectVisibility.Public)
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
        foreach (var project in projects)
        {
            await _git.InitRepositoryAsync(project.Id);
        }

        return projects.Select(ToDto).ToList();
    }

    public async Task<List<ProjectRes>> SearchProjectsAsync(string query)
    {
        var lower = query.ToLower();
        var projects = await _db.Projects.Include(p => p.Owner)
            .Where(p => p.Visibility == ProjectVisibility.Public &&
                        (p.Name.ToLower().Contains(lower) ||
                         (p.Description != null && p.Description.ToLower().Contains(lower))))
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
        foreach (var project in projects)
        {
            await _git.InitRepositoryAsync(project.Id);
        }

        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectRes> UpdateProjectAsync(Guid projectId, Guid userId, UpdateProjectReq dto)
    {
        var project = await _db.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        if (project.OwnerId != userId) throw new UnauthorizedAccessException("Only owner can update project");
        if (dto.Name != null) project.Name = dto.Name;
        if (dto.Description != null) project.Description = dto.Description;
        if (dto.Visibility != null)
            project.Visibility = dto.Visibility.ToLower() == "private" ? ProjectVisibility.Private : ProjectVisibility.Public;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(project);
    }

    public async Task DeleteProjectAsync(Guid projectId, Guid userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        if (project.OwnerId != userId) throw new UnauthorizedAccessException("Only owner can delete project");
        _git.DeleteRepository(project.Id);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
    }

    public async Task AddMemberAsync(Guid projectId, Guid userId, AddMemberReq dto)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        if (project.OwnerId != userId) throw new UnauthorizedAccessException("Only owner can add members");
        var targetUser = await _userManager.FindByNameAsync(dto.Username);
        if (targetUser == null) throw new KeyNotFoundException("User not found");
        var exists = await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUser.Id);
        if (exists) throw new InvalidOperationException("User is already a member");
        var role = dto.Role?.ToLower() == "writer" ? ProjectRole.Writer : ProjectRole.Reader;
        _db.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = targetUser.Id,
            Role = role,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid ownerId, Guid memberId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        if (project.OwnerId != ownerId) throw new UnauthorizedAccessException("Only owner can remove members");
        var member = await _db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberId);
        if (member == null) throw new KeyNotFoundException("Member not found");
        _db.ProjectMembers.Remove(member);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MemberRes>> GetMembersAsync(Guid projectId, Guid? userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        if (!await HasReadAccessAsync(project, userId)) throw new UnauthorizedAccessException("No access");
        var members = await _db.ProjectMembers.Include(m => m.User)
            .Where(m => m.ProjectId == projectId).ToListAsync();
        return members.Select(m => new MemberRes(m.UserId, m.User.UserName ?? "", m.Role.ToString().ToLower())).ToList();
    }

    public async Task<bool> HasReadAccessAsync(Guid projectId, Guid? userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) return false;
        return await HasReadAccessAsync(project, userId);
    }

    public async Task<bool> HasWriteAccessAsync(Guid projectId, Guid userId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) return false;
        if (project.OwnerId == userId) return true;
        return await _db.ProjectMembers.AnyAsync(m =>
            m.ProjectId == projectId && m.UserId == userId && m.Role == ProjectRole.Writer);
    }

    private async Task<bool> HasReadAccessAsync(Project project, Guid? userId)
    {
        if (project.Visibility == ProjectVisibility.Public) return true;
        if (userId == null) return false;
        if (project.OwnerId == userId.Value) return true;
        return await _db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == userId.Value);
    }

    private static ProjectRes ToDto(Project p) => new(
        p.Id, p.Name, p.Description,
        p.Visibility.ToString().ToLower(),
        p.DefaultBranch, p.Owner?.UserName ?? "",
        p.CreatedAtUtc);
}
