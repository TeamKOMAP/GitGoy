using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Vcs.Domain.Entities;
using Vcs.Infrastructure;

namespace Vcs.Api.Controllers;

[ApiController, Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, IConfiguration c)
    {
        _userManager = um; _signInManager = sm; _config = c;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterReq dto)
    {
        var user = new ApplicationUser { UserName = dto.Username, Email = dto.Email, CreatedAtUtc = DateTime.UtcNow };
        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);
        return Ok(new { message = "User registered successfully" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginReq dto)
    {
        var user = await _userManager.FindByNameAsync(dto.Username);
        if (user == null) return Unauthorized(new { message = "Invalid username or password" });
        if (!(await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false)).Succeeded)
            return Unauthorized(new { message = "Invalid username or password" });

        var token = GenerateToken(user);
        return Ok(new AuthRes(token, user.UserName ?? "", user.Email ?? ""));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == null) return Unauthorized();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.UserName, user.Email, user.CreatedAtUtc });
    }

    private string GenerateToken(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"], audience: _config["Jwt:Audience"],
            claims: claims, expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:ExpiryMinutes"])),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
