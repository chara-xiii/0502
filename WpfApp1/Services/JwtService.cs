using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public class JwtPayload
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public static class JwtService
    {
        private static readonly string SessionSecret = CreateSessionSecret();

        private static string CreateSessionSecret()
        {
            var bytes = new byte[32];
            using (var generator = RandomNumberGenerator.Create())
                generator.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string Secret
        {
            get
            {
                var value = Environment.GetEnvironmentVariable("POSTOFFICE_JWT_SECRET");
                return string.IsNullOrWhiteSpace(value) ? SessionSecret : value;
            }
        }

        public static string CreateToken(User user, int hours = 8)
        {
            var header = new Dictionary<string, object>
            {
                { "alg", "HS256" },
                { "typ", "JWT" }
            };

            var now = DateTimeOffset.UtcNow;
            var payload = new Dictionary<string, object>
            {
                { "sub", user.UserId },
                { "name", user.FullName },
                { "role", user.Role },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", now.AddHours(hours).ToUnixTimeSeconds() }
            };

            var serializer = new JavaScriptSerializer();
            var headerPart = Base64UrlEncode(Encoding.UTF8.GetBytes(serializer.Serialize(header)));
            var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(serializer.Serialize(payload)));
            var signature = Sign(headerPart + "." + payloadPart);
            return headerPart + "." + payloadPart + "." + signature;
        }

        public static JwtPayload Validate(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var expected = Sign(parts[0] + "." + parts[1]);
            if (!FixedTimeEquals(expected, parts[2])) return null;

            try
            {
                var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                var serializer = new JavaScriptSerializer();
                var data = serializer.Deserialize<Dictionary<string, object>>(json);
                if (data == null) return null;

                var exp = Convert.ToInt64(data["exp"]);
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
                if (expiresAt <= DateTime.UtcNow) return null;

                return new JwtPayload
                {
                    UserId = Convert.ToInt32(data["sub"]),
                    Name = Convert.ToString(data["name"]),
                    Role = Convert.ToString(data["role"]),
                    ExpiresAt = expiresAt
                };
            }
            catch
            {
                return null;
            }
        }

        private static string Sign(string data)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret)))
            {
                return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
            }
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var diff = 0;
            for (var i = 0; i < left.Length; i++) diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
