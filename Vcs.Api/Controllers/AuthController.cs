using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Vcs.Domain.Entities;
using Vcs.Infrastructure;

namespace Vcs.Api.Controllers;

[ApiController, Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginReq dto)
    {
        var user = await FindOrCreateUserAsync(dto.Username);
        return Ok(new AuthRes(user.UserName ?? string.Empty, user.Email ?? string.Empty));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var username = Request.Headers["X-User-Name"].FirstOrDefault() ?? "desktop-client";
        var user = await FindOrCreateUserAsync(username);
        return Ok(new { user.Id, user.UserName, user.Email, user.CreatedAtUtc });
    }

    private async Task<ApplicationUser> FindOrCreateUserAsync(string? username)
    {
        var normalizedUsername = string.IsNullOrWhiteSpace(username)
            ? "desktop-client"
            : username.Trim();

        var user = await _userManager.FindByNameAsync(normalizedUsername);
        if (user is not null)
        {
            return user;
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

        return user;
    }
}
