using System;
using System.Security.Cryptography;
using System.Text;

namespace WpfApp1.Services
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;

        public static string Hash(string password)
        {
            var salt = new byte[SaltSize];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(salt);
            }

            using (var derive = new Rfc2898DeriveBytes(password ?? string.Empty, salt, Iterations))
            {
                var hash = derive.GetBytes(HashSize);
                return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
            }
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return false;
            var parts = stored.Split(':');
            if (parts.Length != 2) return false;

            try
            {
                var salt = Convert.FromBase64String(parts[0]);
                var expected = Convert.FromBase64String(parts[1]);
                using (var derive = new Rfc2898DeriveBytes(password ?? string.Empty, salt, Iterations))
                {
                    var actual = derive.GetBytes(expected.Length);
                    return FixedTimeEquals(actual, expected);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var diff = 0;
            for (var i = 0; i < left.Length; i++) diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
