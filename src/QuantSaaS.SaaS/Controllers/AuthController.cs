using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.SaaS.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly QuantDbContext _db;
    private readonly Services.JwtService _jwt;

    public AuthController(QuantDbContext db, Services.JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AuthRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { error = "Email already registered" });

        var user = new UserEntity
        {
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "user"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user.Id, user.Email, user.Role);
        return Ok(new { token, userId = user.Id, email = user.Email, role = user.Role });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        var token = _jwt.GenerateToken(user.Id, user.Email, user.Role);
        return Ok(new { token, userId = user.Id, email = user.Email, role = user.Role });
    }
}

public record AuthRequest(string Email, string Password);
