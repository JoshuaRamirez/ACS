using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ACS.Service.Data;
using ACS.Service.Data.Models;

namespace ACS.Service.Services;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(string email, string password, string tenantId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<IEnumerable<string>> GetUserRolesAsync(int userId);
    Task RecordSuccessfulLoginAsync(int userId);
    Task RecordFailedLoginAsync(string email);
    Task<bool> IsAccountLockedAsync(string email);
    Task UnlockAccountAsync(string email);
    Task<User> CreateUserAsync(string name, string email, string password, string tenantId, string createdBy);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ILogger<AuthenticationService> _logger;
    
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);

    public AuthenticationService(
        ApplicationDbContext dbContext,
        IPasswordHashService passwordHashService,
        ILogger<AuthenticationService> logger)
    {
        _dbContext = dbContext;
        _passwordHashService = passwordHashService;
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string email, string password, string tenantId)
    {
        try
        {
            // Get user by email
            var user = await GetUserByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Authentication failed: User not found for email {Email}", email);
                return AuthenticationResult.Failed("Invalid credentials");
            }

            // Check if account is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Authentication failed: User account inactive for {Email}", email);
                return AuthenticationResult.Failed("Account is disabled");
            }

            // Check if account is locked
            if (await IsAccountLockedAsync(email))
            {
                _logger.LogWarning("Authentication failed: Account locked for {Email}", email);
                return AuthenticationResult.Failed("Account is locked due to too many failed attempts");
            }

            // Verify password
            if (!_passwordHashService.VerifyPassword(password, user.PasswordHash, user.Salt))
            {
                await RecordFailedLoginAsync(email);
                _logger.LogWarning("Authentication failed: Invalid password for {Email}", email);
                return AuthenticationResult.Failed("Invalid credentials");
            }

            // Get user roles
            var roles = await GetUserRolesAsync(user.Id);

            // Record successful login
            await RecordSuccessfulLoginAsync(user.Id);

            _logger.LogInformation("Authentication successful for user {Email}", email);
            
            return AuthenticationResult.Success(user.Id, user.Name, user.Email, tenantId, roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for {Email}", email);
            return AuthenticationResult.Failed("Authentication service error");
        }
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(int userId)
    {
        var roles = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        return roles;
    }

    public async Task RecordSuccessfulLoginAsync(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.FailedLoginAttempts = 0;
            user.LockedOutUntil = null;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task RecordFailedLoginAsync(string email)
    {
        var user = await GetUserByEmailAsync(email);
        if (user != null)
        {
            user.FailedLoginAttempts++;
            
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedOutUntil = DateTime.UtcNow.Add(LockoutDuration);
                _logger.LogWarning("Account locked for user {Email} due to {Attempts} failed attempts", 
                    email, user.FailedLoginAttempts);
            }
            
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> IsAccountLockedAsync(string email)
    {
        var user = await GetUserByEmailAsync(email);
        if (user?.LockedOutUntil == null)
            return false;

        if (user.LockedOutUntil > DateTime.UtcNow)
            return true;

        // Lockout period has expired, unlock the account
        user.LockedOutUntil = null;
        user.FailedLoginAttempts = 0;
        await _dbContext.SaveChangesAsync();
        
        return false;
    }

    public async Task UnlockAccountAsync(string email)
    {
        var user = await GetUserByEmailAsync(email);
        if (user != null)
        {
            user.LockedOutUntil = null;
            user.FailedLoginAttempts = 0;
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Account unlocked for user {Email}", email);
        }
    }

    public async Task<User> CreateUserAsync(string name, string email, string password, string tenantId, string createdBy)
    {
        // Check if user already exists
        if (await GetUserByEmailAsync(email) != null)
        {
            throw new InvalidOperationException($"User with email {email} already exists");
        }

        // Create entity first
        var entity = new Entity
        {
            EntityType = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Hash password
        var passwordHash = _passwordHashService.HashPassword(password, out var salt);

        // Create user
        var user = new User
        {
            Name = name,
            Email = email.ToLower(),
            PasswordHash = passwordHash,
            Salt = salt,
            EntityId = entity.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created user {Email} by {CreatedBy}", email, createdBy);
        
        return user;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Password change failed: User {UserId} not found", userId);
            return false;
        }

        // Verify current password
        if (!_passwordHashService.VerifyPassword(currentPassword, user.PasswordHash, user.Salt))
        {
            _logger.LogWarning("Password change failed: Invalid current password for user {UserId}", userId);
            return false;
        }

        // Hash new password
        var newPasswordHash = _passwordHashService.HashPassword(newPassword, out var newSalt);
        
        user.PasswordHash = newPasswordHash;
        user.Salt = newSalt;
        user.UpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Password changed for user {UserId}", userId);
        
        return true;
    }
}

public class AuthenticationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public IEnumerable<string> Roles { get; init; } = Array.Empty<string>();

    public static AuthenticationResult Success(int userId, string userName, string email, string tenantId, IEnumerable<string> roles)
    {
        return new AuthenticationResult
        {
            IsSuccess = true,
            UserId = userId,
            UserName = userName,
            Email = email,
            TenantId = tenantId,
            Roles = roles
        };
    }

    public static AuthenticationResult Failed(string errorMessage)
    {
        return new AuthenticationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}