using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShaydreCrm.Api.Models;
namespace ShaydreCrm.Api.Services;

public class TokenService(IConfiguration cfg)
{
    public LoginResponse Create(AdminUser u)
    {
        var expires = DateTime.UtcNow.AddMinutes(cfg.GetValue("Jwt:ExpiryMinutes", 30));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(cfg["Jwt:Issuer"], cfg["Jwt:Audience"],
            new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Name, u.Username),
                    new Claim(ClaimTypes.Role, u.Role) },
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), u.FullName, expires);
    }
}
