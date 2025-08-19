using System.Security.Cryptography;

namespace ACS.Service.Services;

public interface IPasswordHashService
{
    string HashPassword(string password, out string salt);
    string HashPassword(string password, string salt);
    bool VerifyPassword(string password, string hash, string? salt);
}

public class PasswordHashService : IPasswordHashService
{
    private const int SaltSize = 32; // 256 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 100000; // OWASP recommended minimum

    public string HashPassword(string password, out string salt)
    {
        // Generate salt
        using var rng = RandomNumberGenerator.Create();
        var saltBytes = new byte[SaltSize];
        rng.GetBytes(saltBytes);
        salt = Convert.ToBase64String(saltBytes);
        
        return HashPassword(password, salt);
    }

    public string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        
        // Create hash
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(HashSize);
        
        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyPassword(string password, string hash, string? salt)
    {
        if (string.IsNullOrEmpty(salt))
        {
            // Legacy support for passwords without salt
            return hash == HashPasswordLegacy(password);
        }
        
        var computedHash = HashPassword(password, salt);
        return computedHash == hash;
    }
    
    private string HashPasswordLegacy(string password)
    {
        // Simple SHA256 for legacy support (not recommended for new implementations)
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashBytes);
    }
}