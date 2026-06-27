using Microsoft.AspNetCore.Mvc;
using Vcs.Infrastructure;
using Vcs.Infrastructure.Services;

namespace Vcs.Api.Controllers;

[ApiController, Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _svc;
    public ProjectsController(ProjectService svc) => _svc = svc;
    private Task<Guid> GetUserIdAsync() => _svc.GetOrCreateUserIdAsync(Request.Headers["X-User-Name"].FirstOrDefault());

    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateProjectReq dto)
    {
        try
        {
            var project = await _svc.CreateProjectAsync(await GetUserIdAsync(), dto);
            return Created($"/api/projects/{project.Id}", project);
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("my")] public async Task<IActionResult> GetMy() => Ok(await _svc.GetMyProjectsAsync(await GetUserIdAsync()));
    [HttpGet("public")] public async Task<IActionResult> GetPublic() => Ok(await _svc.GetPublicProjectsAsync());

    [HttpGet("search")] public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<ProjectRes>());
        return Ok(await _svc.SearchProjectsAsync(q));
    }

    [HttpGet("{id}")] public async Task<IActionResult> Get(Guid id)
    {
        var p = await _svc.GetProjectAsync(id, await GetUserIdAsync());
        return p == null ? NotFound() : Ok(p);
    }

    [HttpPatch("{id}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectReq dto)
    {
        try { return Ok(await _svc.UpdateProjectAsync(id, await GetUserIdAsync(), dto)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id}")] public async Task<IActionResult> Delete(Guid id)
    {
        try { await _svc.DeleteProjectAsync(id, await GetUserIdAsync()); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{id}/members")] public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberReq dto)
    {
        try { await _svc.AddMemberAsync(id, await GetUserIdAsync(), dto); return Ok(new { message = "Member added" }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpGet("{id}/members")] public async Task<IActionResult> GetMembers(Guid id)
    {
        try { return Ok(await _svc.GetMembersAsync(id, await GetUserIdAsync())); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id}/members/{memberId}")] public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        try { await _svc.RemoveMemberAsync(id, await GetUserIdAsync(), memberId); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
