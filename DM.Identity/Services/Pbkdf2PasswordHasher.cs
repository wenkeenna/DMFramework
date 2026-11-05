using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Services
{
    /// <summary>
    /// 存储格式：v1$迭代次数$盐$哈希（Base64）。迭代次数写在记录里，以后调大不影响旧账号。
    /// 比较用固定时间算法，避免通过耗时差异推测哈希内容。
    /// </summary>
    /// <summary>存储格式：v1$迭代次数$盐$哈希（Base64）。迭代次数写在记录里，以后调大不影响旧账号。比较用固定时间算法。</summary>
    public sealed class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const int Iterations = 100000, SaltSize = 16, HashSize = 32;

        public string Hash(string password)
        {
            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return "v1$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(Derive(password, salt, Iterations));
        }

        public bool Verify(string password, string stored)
        {
            try
            {
                string[] p = (stored ?? "").Split('$');
                if (p.Length != 4 || p[0] != "v1") return false;
                byte[] expected = Convert.FromBase64String(p[3]);
                byte[] actual = Derive(password ?? "", Convert.FromBase64String(p[2]), int.Parse(p[1]));
                return FixedEquals(expected, actual);
            }
            catch (Exception) { return false; }             // 格式损坏一律当作校验失败
        }

        private static byte[] Derive(string pw, byte[] salt, int iter)
        {
            using (var kdf = new Rfc2898DeriveBytes(pw, salt, iter, HashAlgorithmName.SHA256)) return kdf.GetBytes(HashSize);
        }

        private static bool FixedEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int d = 0;
            for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i];
            return d == 0;
        }
    }
}
