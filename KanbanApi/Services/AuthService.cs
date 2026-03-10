using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KanbanApi.Services;

public class AuthService(AppDbContext db, IOptions<JwtSettings> jwtOptions, ILogger<AuthService> logger) : IAuthService
{
    public async Task<string?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for username {Username}", username);
            return null;
        }

        logger.LogInformation("User {Username} logged in", username);
        return GenerateToken(user);
    }

    public async Task<ServiceResult<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Username == request.Username, ct))
        {
            logger.LogWarning("Attempted to create duplicate user {Username}", request.Username);
            return ServiceResult<UserResponse>.Conflict();
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created user {Username} with role {Role}", user.Username, user.Role);
        return ServiceResult<UserResponse>.Ok(new UserResponse(user.Id, user.Username, user.Role));
    }

    public async Task<IEnumerable<UserResponse>> GetUsersAsync(CancellationToken ct = default)
    {
        return await db.Users
            .Select(u => new UserResponse(u.Id, u.Username, u.Role))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult> DeleteUserAsync(int id, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return ServiceResult.NotFound();

        if (user.Role == "admin")
        {
            var adminCount = await db.Users.CountAsync(u => u.Role == "admin", ct);
            if (adminCount <= 1)
            {
                logger.LogWarning("Attempted to delete last admin user {Username}", user.Username);
                return ServiceResult.Conflict();
            }
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted user {Username}", user.Username);
        return ServiceResult.Ok();
    }

    private string GenerateToken(User user)
    {
        var jwt = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
