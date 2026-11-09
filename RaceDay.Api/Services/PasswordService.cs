using System.Security.Cryptography;

namespace RaceDay.Api.Services;

public interface IPasswordService
{
    /// <summary>Returns a salted PBKDF2 hash string suitable for storing in Users.PasswordHash.</summary>
    string Hash(string password);

    /// <summary>Checks a plain password against a stored hash string.</summary>
    bool Verify(string password, string storedHash);
}

/// <summary>
/// PBKDF2 (HMAC-SHA256) with a random 16-byte salt per password.
/// Stored format: PBKDF2-SHA256$iterations$base64(salt)$base64(hash)
/// </summary>
public class PasswordService : IPasswordService
{
    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix) return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
