using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vcs.Infrastructure;
using Vcs.Infrastructure.Services;

namespace Vcs.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _svc;
    public ProjectsController(ProjectService svc) => _svc = svc;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateProjectReq dto)
    {
        try { return CreatedAtAction(nameof(Get), new { id = (await _svc.CreateProjectAsync(UserId, dto)).Id }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("my")] public async Task<IActionResult> GetMy() => Ok(await _svc.GetMyProjectsAsync(UserId));
    [HttpGet("public")] public async Task<IActionResult> GetPublic() => Ok(await _svc.GetPublicProjectsAsync());

    [HttpGet("search")] public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<ProjectRes>());
        return Ok(await _svc.SearchProjectsAsync(q));
    }

    [HttpGet("{id}")] public async Task<IActionResult> Get(Guid id)
    {
        var p = await _svc.GetProjectAsync(id, UserId);
        return p == null ? NotFound() : Ok(p);
    }

    [HttpPatch("{id}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectReq dto)
    {
        try { return Ok(await _svc.UpdateProjectAsync(id, UserId, dto)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id}")] public async Task<IActionResult> Delete(Guid id)
    {
        try { await _svc.DeleteProjectAsync(id, UserId); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{id}/members")] public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberReq dto)
    {
        try { await _svc.AddMemberAsync(id, UserId, dto); return Ok(new { message = "Member added" }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpGet("{id}/members")] public async Task<IActionResult> GetMembers(Guid id)
    {
        try { return Ok(await _svc.GetMembersAsync(id, UserId)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id}/members/{memberId}")] public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        try { await _svc.RemoveMemberAsync(id, UserId, memberId); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
