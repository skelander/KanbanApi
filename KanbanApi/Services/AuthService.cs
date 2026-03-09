using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace KanbanApi.Services;

public class AuthService(AppDbContext db, IConfiguration config, ILogger<AuthService> logger) : IAuthService
{
    public async Task<string?> LoginAsync(string username, string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for username {Username}", username);
            return null;
        }

        logger.LogInformation("User {Username} logged in", username);
        return GenerateToken(user);
    }

    public async Task<ServiceResult<UserResponse>> CreateUserAsync(CreateUserRequest request)
    {
        if (!CreateUserRequest.AllowedRoles.Contains(request.Role))
            return ServiceResult<UserResponse>.Forbidden();

        if (await db.Users.AnyAsync(u => u.Username == request.Username))
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
        await db.SaveChangesAsync();

        logger.LogInformation("Created user {Username} with role {Role}", user.Username, user.Role);
        return ServiceResult<UserResponse>.Ok(new UserResponse(user.Id, user.Username, user.Role));
    }

    public async Task<IEnumerable<UserResponse>> GetUsersAsync()
    {
        return await db.Users
            .Select(u => new UserResponse(u.Id, u.Username, u.Role))
            .ToListAsync();
    }

    public async Task<ServiceResult<bool>> DeleteUserAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return ServiceResult<bool>.NotFound();

        if (user.Role == "admin")
        {
            var adminCount = await db.Users.CountAsync(u => u.Role == "admin");
            if (adminCount <= 1)
            {
                logger.LogWarning("Attempted to delete last admin user {Username}", user.Username);
                return ServiceResult<bool>.Conflict();
            }
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted user {Username}", user.Username);
        return ServiceResult<bool>.Ok(true);
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
