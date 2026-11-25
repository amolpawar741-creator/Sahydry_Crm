using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaydreCrm.Api.Data;
using ShaydreCrm.Api.Models;
using ShaydreCrm.Api.Services;
namespace ShaydreCrm.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest r)
    {
        var u = await db.AdminUsers.FirstOrDefaultAsync(x => x.Username == r.Username && x.IsActive);
        if (u is null || !BCrypt.Net.BCrypt.Verify(r.Password, u.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password" });
        db.AuditLogs.Add(new AuditLog { UserName = u.Username, Action = "Login" });
        await db.SaveChangesAsync();
        return tokens.Create(u);
    }
}
