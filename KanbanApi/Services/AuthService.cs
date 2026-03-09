using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KanbanApi.Services;

public class AuthService(AppDbContext db, IConfiguration config) : IAuthService
{
    public async Task<string?> LoginAsync(string username, string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return GenerateToken(user);
    }

    public async Task<ServiceResult<UserResponse>> CreateUserAsync(CreateUserRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Username == request.Username))
            return ServiceResult<UserResponse>.Forbidden();

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return ServiceResult<UserResponse>.Ok(new UserResponse(user.Id, user.Username, user.Role));
    }

    public async Task<IEnumerable<UserResponse>> GetUsersAsync()
    {
        return await db.Users
            .Select(u => new UserResponse(u.Id, u.Username, u.Role))
            .ToListAsync();
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return false;

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return true;
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
