using System.Security.Cryptography;
using System.Text;
using ClassBook.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ClassBook.Infrastructure.Security
{
    /// <summary>
    /// Современный хэшер паролей на базе ASP.NET Identity.
    /// Старые SHA-256 хэши поддерживаются только для входа и автоматически
    /// заменяются на новый формат после успешной авторизации.
    /// </summary>
    public class AspNetIdentityPasswordHasherAdapter : IPasswordHasher
    {
        private readonly PasswordHasher<object> _passwordHasher = new();

        public string Hash(string password)
        {
            return _passwordHasher.HashPassword(new object(), password);
        }

        public bool Verify(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                return false;

            if (NeedsRehash(hash))
                return VerifyLegacySha256(password, hash);

            var result = _passwordHasher.VerifyHashedPassword(new object(), hash, password);
            return result == PasswordVerificationResult.Success
                || result == PasswordVerificationResult.SuccessRehashNeeded;
        }

        public bool NeedsRehash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return true;

            try
            {
                var bytes = Convert.FromBase64String(hash);
                return bytes.Length == 32;
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyLegacySha256(string password, string hash)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var legacyHash = Convert.ToBase64String(bytes);
            return legacyHash == hash;
        }
    }
}
