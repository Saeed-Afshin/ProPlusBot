using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProPlusBot.Configuration;

namespace ProPlusBot.Auth;

public class AdminJwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(IEnumerable<Claim> claims)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret) || _options.Secret.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Secret must be at least 32 characters. Set Jwt__Secret in environment or user secrets.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(_options.ExpirationDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
