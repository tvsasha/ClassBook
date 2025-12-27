using System.Security.Cryptography;
using System.Text;
using ClassBook.Domain.Interfaces;

namespace ClassBook.Infrastructure.Security
{
    public class Sha256PasswordHasherAdapter : IPasswordHasher
    {
        public string Hash(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public bool Verify(string password, string hash)
        {
            return Hash(password) == hash;
        }
    }
}