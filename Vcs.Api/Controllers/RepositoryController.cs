using Microsoft.AspNetCore.Mvc;
using Vcs.Infrastructure;
using Vcs.Infrastructure.Services;

namespace Vcs.Api.Controllers;

[ApiController, Route("api/projects/{projectId}")]
public class RepositoryController : ControllerBase
{
    private readonly GitService _git;
    private readonly ProjectService _projects;

    public RepositoryController(GitService git, ProjectService projects)
    {
        _git = git;
        _projects = projects;
    }

    private string UserName => Request.Headers["X-User-Name"].FirstOrDefault() ?? "desktop-client";
    private Task<Guid> GetUserIdAsync() => _projects.GetOrCreateUserIdAsync(UserName);

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(Guid projectId)
    {
        if (!await _projects.HasReadAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try { return Ok(await _git.GetBranchesAsync(projectId)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("branches")]
    public async Task<IActionResult> CreateBranch(Guid projectId, [FromBody] CreateBranchReq dto)
    {
        if (!await _projects.HasWriteAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try { await _git.CreateBranchAsync(projectId, dto.Name, dto.SourceBranch); return Ok(new { message = "Branch created" }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("branches/{name}")]
    public async Task<IActionResult> RenameBranch(Guid projectId, string name, [FromBody] RenameBranchReq dto)
    {
        if (!await _projects.HasWriteAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try { await _git.RenameBranchAsync(projectId, name, dto.Name); return Ok(new { name = dto.Name }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("branches/{name}")]
    public async Task<IActionResult> DeleteBranch(Guid projectId, string name)
    {
        if (!await _projects.HasWriteAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try { await _git.DeleteBranchAsync(projectId, name); return NoContent(); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("commits")]
    public async Task<IActionResult> GetCommits(Guid projectId, [FromQuery] string branch = "main", [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        if (!await _projects.HasReadAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try { return Ok(await _git.GetCommitsAsync(projectId, branch, skip, take)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("commits/{sha}")]
    public async Task<IActionResult> GetCommit(Guid projectId, string sha)
    {
        if (!await _projects.HasReadAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        var commit = await _git.GetCommitAsync(projectId, sha);
        return commit == null ? NotFound() : Ok(commit);
    }

    [HttpPost("commits")]
    public async Task<IActionResult> CreateCommit(Guid projectId, [FromBody] CreateCommitReq dto)
    {
        if (!await _projects.HasWriteAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try
        {
            var sha = await _git.CreateCommitAsync(projectId, dto.Branch, dto.Message, UserName, dto.Changes);
            return Ok(new { sha });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("files")]
    public async Task<IActionResult> GetFiles(Guid projectId, [FromQuery] string branch = "main", [FromQuery] string? path = null)
    {
        if (!await _projects.HasReadAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try { return Ok(await _git.GetFilesAsync(projectId, branch, path)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("diff")]
    public async Task<IActionResult> GetDiff(Guid projectId, [FromQuery] string? from, [FromQuery] string to)
    {
        if (!await _projects.HasReadAccessAsync(projectId, await GetUserIdAsync())) return Forbid();
        try { return Ok(await _git.GetDiffAsync(projectId, from, to)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}
