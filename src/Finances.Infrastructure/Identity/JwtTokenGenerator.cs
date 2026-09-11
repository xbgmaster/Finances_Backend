using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Finances.Application.Common;
using Microsoft.IdentityModel.Tokens;

namespace Finances.Infrastructure.Identity;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "FinancesApi";
    public string Audience { get; set; } = "FinancesClient";

    /// <summary>Lifetime of the short-lived access token (JWT), in minutes.</summary>
    public int ExpiryMinutes { get; set; } = 15;

    /// <summary>Lifetime of a refresh token, in minutes (rotated on each use).</summary>
    public int RefreshTokenExpiryMinutes { get; set; } = 720;
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(JwtSettings settings) => _settings = settings;

    public TokenResult Generate(string userId, string email, IEnumerable<string> roles)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
