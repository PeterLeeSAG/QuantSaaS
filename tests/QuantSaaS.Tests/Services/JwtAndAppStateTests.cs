using System.IdentityModel.Tokens.Jwt;
using QuantSaaS.Core.Config;
using QuantSaaS.SaaS.Services;

namespace QuantSaaS.Tests.Services;

public class JwtServiceTests
{
    private static JwtConfig ValidConfig() => new()
    {
        Secret = "super-secret-key-for-testing-1234567890!!",
        ExpiryHours = 1
    };

    // ── GenerateToken ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var svc = new JwtService(ValidConfig());
        var token = svc.GenerateToken(Guid.NewGuid(), "user@test.com", "user");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_IsValidJwt()
    {
        var svc = new JwtService(ValidConfig());
        var token = svc.GenerateToken(Guid.NewGuid(), "user@test.com", "user");
        // A JWT has 3 dot-separated parts
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void GenerateToken_ContainsEmailClaim()
    {
        var svc = new JwtService(ValidConfig());
        var email = "alice@example.com";
        var token = svc.GenerateToken(Guid.NewGuid(), email, "user");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Value == email);
    }

    [Fact]
    public void GenerateToken_ContainsRoleClaim()
    {
        var svc = new JwtService(ValidConfig());
        var token = svc.GenerateToken(Guid.NewGuid(), "admin@example.com", "admin");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Value == "admin");
    }

    [Fact]
    public void GenerateToken_ContainsUserIdClaim()
    {
        var svc = new JwtService(ValidConfig());
        var userId = Guid.NewGuid();
        var token = svc.GenerateToken(userId, "u@t.com", "user");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Value == userId.ToString());
    }

    [Fact]
    public void GenerateToken_DifferentUsersProduceDifferentTokens()
    {
        var svc = new JwtService(ValidConfig());
        var t1 = svc.GenerateToken(Guid.NewGuid(), "a@a.com", "user");
        var t2 = svc.GenerateToken(Guid.NewGuid(), "b@b.com", "user");
        Assert.NotEqual(t1, t2);
    }

    // ── ValidateToken ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_ValidToken_ReturnsPrincipal()
    {
        var svc = new JwtService(ValidConfig());
        var token = svc.GenerateToken(Guid.NewGuid(), "u@t.com", "user");
        var principal = svc.ValidateToken(token);
        Assert.NotNull(principal);
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        var svc = new JwtService(ValidConfig());
        var principal = svc.ValidateToken("not.a.valid.token");
        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_WrongSecret_ReturnsNull()
    {
        var svc = new JwtService(ValidConfig());
        var token = svc.GenerateToken(Guid.NewGuid(), "u@t.com", "user");

        var wrongConfig = new JwtConfig { Secret = "completely-different-secret-key!!", ExpiryHours = 1 };
        var svc2 = new JwtService(wrongConfig);
        var principal = svc2.ValidateToken(token);
        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_EmptyString_ReturnsNull()
    {
        var svc = new JwtService(ValidConfig());
        Assert.Null(svc.ValidateToken(""));
    }

    [Fact]
    public void ValidateToken_RoundTrip_PreservesUserId()
    {
        var svc = new JwtService(ValidConfig());
        var userId = Guid.NewGuid();
        var token = svc.GenerateToken(userId, "round@trip.com", "admin");
        var principal = svc.ValidateToken(token);
        Assert.NotNull(principal);
        var idClaim = principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        Assert.NotNull(idClaim);
        Assert.Equal(userId.ToString(), idClaim!.Value);
    }
}

public class AppStateTests
{
    [Fact]
    public void Initial_IsNotAuthenticated()
    {
        var state = new AppState();
        Assert.False(state.IsAuthenticated);
        Assert.Null(state.JwtToken);
        Assert.Null(state.UserEmail);
        Assert.Null(state.UserId);
    }

    [Fact]
    public void SetAuth_IsAuthenticated()
    {
        var state = new AppState();
        state.SetAuth("mytoken", "u@t.com", Guid.NewGuid());
        Assert.True(state.IsAuthenticated);
        Assert.Equal("mytoken", state.JwtToken);
        Assert.Equal("u@t.com", state.UserEmail);
    }

    [Fact]
    public void ClearAuth_IsNotAuthenticated()
    {
        var state = new AppState();
        state.SetAuth("mytoken", "u@t.com", Guid.NewGuid());
        state.ClearAuth();
        Assert.False(state.IsAuthenticated);
        Assert.Null(state.JwtToken);
        Assert.Null(state.UserEmail);
        Assert.Null(state.UserId);
    }

    [Fact]
    public void SetAuth_FiresOnChange()
    {
        var state = new AppState();
        bool fired = false;
        state.OnChange += () => fired = true;
        state.SetAuth("token", "u@t.com", Guid.NewGuid());
        Assert.True(fired);
    }

    [Fact]
    public void ClearAuth_FiresOnChange()
    {
        var state = new AppState();
        bool fired = false;
        state.SetAuth("token", "u@t.com", Guid.NewGuid());
        state.OnChange += () => fired = true;
        state.ClearAuth();
        Assert.True(fired);
    }

    [Fact]
    public void SetAuth_UpdatesUserId()
    {
        var state = new AppState();
        var id = Guid.NewGuid();
        state.SetAuth("tok", "a@b.com", id);
        Assert.Equal(id, state.UserId);
    }

    [Fact]
    public void MultipleSetAuth_UpdatesAllFields()
    {
        var state = new AppState();
        state.SetAuth("tok1", "a@a.com", Guid.NewGuid());
        var id2 = Guid.NewGuid();
        state.SetAuth("tok2", "b@b.com", id2);
        Assert.Equal("tok2", state.JwtToken);
        Assert.Equal("b@b.com", state.UserEmail);
        Assert.Equal(id2, state.UserId);
    }
}
