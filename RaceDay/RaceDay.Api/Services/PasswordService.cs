using System.Security.Cryptography;

namespace RaceDay.Api.Services;

/// <summary>
/// Hashes and verifies passwords using PBKDF2 with a random salt per password.
/// Only the hash is ever stored; the original password cannot be recovered from it.
/// Stored format: iterations.salt.hash (salt and hash are Base64).
/// </summary>
public class PasswordService
{
    private const int SaltSize = 16;                // 128-bit random salt
    private const int KeySize = 32;                 // 256-bit hash
    private const int DefaultIterations = 600_000;  // deliberately slow, to make guessing expensive
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    private readonly int _iterations;

    // The real API uses the default. The unit tests pass a small number so they run quickly.
    public PasswordService(int iterations = DefaultIterations)
    {
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, Algorithm, KeySize);
        return $"{_iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);

            // The iteration count is read from the stored hash, so old hashes keep working if the default changes
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

            // Fixed-time comparison so timing can't leak how much of the hash matched
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}