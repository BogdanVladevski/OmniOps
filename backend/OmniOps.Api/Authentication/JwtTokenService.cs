using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Authentication;

public interface IJwtTokenService
{
    string GenerateToken(IEnumerable<string> scopes, TimeSpan? lifetime = null);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateToken(IEnumerable<string> scopes, TimeSpan? lifetime = null)
    {
        if (!_options.IsConfigured())
        {
            throw new InvalidOperationException(
                "JWT is not configured. Set JWT_SECRET (minimum 32 characters) in .env");
        }

        var scopeList = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (scopeList.Count == 0)
        {
            throw new ArgumentException("At least one scope is required.", nameof(scopes));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(_options.ExpirationMinutes));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "omniops-client"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("scope", string.Join(' ', scopeList))
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
