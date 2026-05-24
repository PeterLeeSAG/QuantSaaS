using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using QuantSaaS.Core.Config;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.Controllers;
using QuantSaaS.SaaS.Services;

namespace QuantSaaS.Tests.Controllers;

public class AuthControllerTests
{
    private static JwtService CreateJwt() => new JwtService(new JwtConfig
    {
        Secret = "test-secret-key-must-be-at-least-32chars!",
        ExpiryHours = 1
    });

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewUser_ReturnsOkWithToken()
    {
        var userId = Guid.NewGuid();
        var users = new Mock<IUserService>();
        users.Setup(s => s.FindByEmailAsync("new@user.com", default)).ReturnsAsync((AuthUserDto?)null);
        users.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), "user", default))
            .ReturnsAsync(new AuthUserDto(userId, "new@user.com", "hash", "user"));

        var ctrl = new AuthController(users.Object, CreateJwt());
        var result = await ctrl.Register(new AuthRequest("new@user.com", "password"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = ok.Value!.GetType().GetProperty("token")?.GetValue(ok.Value);
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token!.ToString()));
    }

    [Fact]
    public async Task Register_ExistingEmail_ReturnsConflict()
    {
        var users = new Mock<IUserService>();
        users.Setup(s => s.FindByEmailAsync("existing@user.com", default))
             .ReturnsAsync(new AuthUserDto(Guid.NewGuid(), "existing@user.com", "hash", "user"));

        var ctrl = new AuthController(users.Object, CreateJwt());
        var result = await ctrl.Register(new AuthRequest("existing@user.com", "password"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Register_ReturnsUserIdAndEmail()
    {
        var userId = Guid.NewGuid();
        var users = new Mock<IUserService>();
        users.Setup(s => s.FindByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync((AuthUserDto?)null);
        users.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), "user", default))
            .ReturnsAsync(new AuthUserDto(userId, "u@test.com", "hash", "user"));

        var ctrl = new AuthController(users.Object, CreateJwt());
        var result = await ctrl.Register(new AuthRequest("u@test.com", "password123"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value!;
        var returnedUserId = (Guid)value.GetType().GetProperty("userId")!.GetValue(value)!;
        var email = (string)value.GetType().GetProperty("email")!.GetValue(value)!;
        Assert.Equal(userId, returnedUserId);
        Assert.Equal("u@test.com", email);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        // Create a real bcrypt hash for "password123"
        var hash = BCrypt.Net.BCrypt.HashPassword("password123");
        var users = new Mock<IUserService>();
        users.Setup(s => s.FindByEmailAsync("user@test.com", default))
             .ReturnsAsync(new AuthUserDto(Guid.NewGuid(), "user@test.com", hash, "user"));

        var ctrl = new AuthController(users.Object, CreateJwt());
        var result = await ctrl.Login(new AuthRequest("user@test.com", "password123"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = ok.Value!.GetType().GetProperty("token")?.GetValue(ok.Value);
        Assert.NotNull(token);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var users = new Mock<IUserService>();
        users.Setup(s => s.FindByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync((AuthUserDto?)null);

        var ctrl = new AuthController(users.Object, CreateJwt());
        var result = await ctrl.Login(new AuthRequest("nobody@test.com", "pass"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("correct-password");
        var users = new Mock<IUserService>();
        users.Setup(s => s.FindByEmailAsync("u@t.com", default))
             .ReturnsAsync(new AuthUserDto(Guid.NewGuid(), "u@t.com", hash, "user"));

        var ctrl = new AuthController(users.Object, CreateJwt());
        var result = await ctrl.Login(new AuthRequest("u@t.com", "wrong-password"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_AdminRole_TokenContainsAdminRole()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("adminpass");
        var users = new Mock<IUserService>();
        users.Setup(s => s.FindByEmailAsync("admin@test.com", default))
             .ReturnsAsync(new AuthUserDto(Guid.NewGuid(), "admin@test.com", hash, "admin"));

        var ctrl = new AuthController(users.Object, CreateJwt());
        var result = await ctrl.Login(new AuthRequest("admin@test.com", "adminpass"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var role = (string)ok.Value!.GetType().GetProperty("role")!.GetValue(ok.Value)!;
        Assert.Equal("admin", role);
    }
}
