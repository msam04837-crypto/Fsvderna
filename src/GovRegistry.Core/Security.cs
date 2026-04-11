using System.Security.Cryptography;
using System.Text;

namespace GovRegistry.Core;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var hash = Convert.FromBase64String(parts[1]);
        var candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, candidate);
    }
}

public static class LocalProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GovRegistry:v1");

    public static string Protect(string plain)
    {
        if (OperatingSystem.IsWindows())
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        return plain;
    }

    public static string Unprotect(string cipher)
    {
        if (OperatingSystem.IsWindows())
        {
            var bytes = Convert.FromBase64String(cipher);
            var dec = ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }

        return cipher;
    }
}
