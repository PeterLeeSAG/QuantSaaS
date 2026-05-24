using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Infrastructure.Services;

namespace QuantSaaS.SaaS.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _users;
    private readonly Services.JwtService _jwt;

    public AuthController(IUserService users, Services.JwtService jwt)
    {
        _users = users;
        _jwt   = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AuthRequest req)
    {
        var existing = await _users.FindByEmailAsync(req.Email);
        if (existing is not null)
            return Conflict(new { error = "Email already registered" });

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        var user = await _users.CreateUserAsync(req.Email, hash);

        var token = _jwt.GenerateToken(user.Id, user.Email, user.Role);
        return Ok(new { token, userId = user.Id, email = user.Email, role = user.Role });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        var token = _jwt.GenerateToken(user.Id, user.Email, user.Role);
        return Ok(new { token, userId = user.Id, email = user.Email, role = user.Role });
    }
}

public record AuthRequest(string Email, string Password);
