using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinAI.Api.Models;
using FinAI.Api.Services.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace FinAI.UnitTests.Services.Auth;

[Trait("Category", "Unit")]
public class TokenServiceTests
{
    private static readonly string SigningKey = "test-signing-key-64-characters-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static TokenService CreateService(int ttlMinutes = 15)
        => new(Options.Create(new JwtOptions
        {
            Issuer = "FinAI",
            Audience = "FinAI.Clients",
            SigningKey = SigningKey,
            AccessTokenTtlMinutes = ttlMinutes
        }));

    private static FinAiUser User() => new()
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Email = "user@example.com",
        UserName = "user@example.com"
    };

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        // Arrange
        var service = CreateService();
        var user = User();

        // Act
        var token = service.GenerateAccessToken(user, ["User", "Admin"]);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "user@example.com");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        jwt.Issuer.Should().Be("FinAI");
        jwt.Audiences.Should().Contain("FinAI.Clients");
    }

    [Fact]
    public void GenerateAccessToken_ExpiresInConfiguredTtl()
    {
        // Arrange
        var service = CreateService(ttlMinutes: 15);

        // Act
        var token = service.GenerateAccessToken(User(), []);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expectedExpiration = DateTime.UtcNow.AddMinutes(15);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Token_WithInvalidSignature_IsRejected()
    {
        // Arrange
        var service = CreateService();
        var token = service.GenerateAccessToken(User(), ["User"]);

        // Act: validar com outra chave
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-key-wrong-key-wrong-key-wrong-key-wrong-key-wrong-key!!"));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "FinAI",
            ValidateAudience = true,
            ValidAudience = "FinAI.Clients",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = wrongKey,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var act = () => handler.ValidateToken(token, parameters, out _);
        act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }

    [Fact]
    public void Token_WithInvalidIssuer_IsRejected()
    {
        // Arrange
        var service = CreateService();
        var token = service.GenerateAccessToken(User(), ["User"]);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "OutroIssuer",
            ValidateAudience = true,
            ValidAudience = "FinAI.Clients",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Act
        var handler = new JwtSecurityTokenHandler();
        var act = () => handler.ValidateToken(token, parameters, out _);

        // Assert
        act.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public void HashToken_IsStableSha256Hex()
    {
        // Arrange
        var service = CreateService();

        // Act
        var hash1 = service.HashToken("abc123");
        var hash2 = service.HashToken("abc123");

        // Assert: SHA-256 hex lowercase de 64 chars, estável
        hash1.Should().Be(hash2);
        hash1.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void GenerateRefreshToken_IsUniqueAndNonEmpty()
    {
        // Arrange
        var service = CreateService();

        // Act
        var t1 = service.GenerateRefreshToken();
        var t2 = service.GenerateRefreshToken();

        // Assert
        t1.Should().NotBeNullOrWhiteSpace();
        t1.Should().NotBe(t2);
    }
}
