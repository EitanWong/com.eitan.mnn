using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Versioned encrypt-then-MAC envelope; independent random salt/IV and separated AES/HMAC keys.</summary>
    public static class MNNProtectedSecrets
    {
        private const int Iterations = 210000;
        public static byte[] Encrypt(string plaintext, string passphrase)
        {
            if (plaintext == null || plaintext.Length > 16384 || string.IsNullOrEmpty(passphrase))
                throw new ArgumentException("A bounded secret and nonempty passphrase are required.");
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(passphrase, salt, Iterations, HashAlgorithmName.SHA256))
            using (var aes = Aes.Create())
            {
                byte[] key = derive.GetBytes(32), macKey = derive.GetBytes(32);
                try
                {
                    aes.Key = key;
                    aes.GenerateIV();
                    byte[] clear = Encoding.UTF8.GetBytes(plaintext), cipher;
                    try
                    {
                        using (var encryptor = aes.CreateEncryptor())
                            cipher = encryptor.TransformFinalBlock(clear, 0, clear.Length);
                    }
                    finally
                    {
                        Array.Clear(clear, 0, clear.Length);
                    }

                    var result = new byte[1 + 16 + 16 + cipher.Length + 32];
                    result[0] = 1;
                    Buffer.BlockCopy(salt, 0, result, 1, 16);
                    Buffer.BlockCopy(aes.IV, 0, result, 17, 16);
                    Buffer.BlockCopy(cipher, 0, result, 33, cipher.Length);
                    using (var mac = new HMACSHA256(macKey))
                        Buffer.BlockCopy(mac.ComputeHash(result, 0, result.Length - 32), 0, result, result.Length - 32, 32);
                    return result;
                }
                finally
                {
                    Array.Clear(key, 0, key.Length);
                    Array.Clear(macKey, 0, macKey.Length);
                }
            }
        }

        public static string Decrypt(byte[] envelope, string passphrase)
        {
            if (envelope == null || envelope.Length < 81 || envelope.Length > 131072 || envelope[0] != 1 || (envelope.Length - 65) % 16 != 0 || string.IsNullOrEmpty(passphrase))
                throw new CryptographicException("Invalid protected credential.");
            var salt = new byte[16];
            Buffer.BlockCopy(envelope, 1, salt, 0, 16);
            using (var derive = new Rfc2898DeriveBytes(passphrase, salt, Iterations, HashAlgorithmName.SHA256))
            using (var aes = Aes.Create())
            {
                byte[] key = derive.GetBytes(32), macKey = derive.GetBytes(32);
                try
                {
                    byte[] digest;
                    using (var mac = new HMACSHA256(macKey))
                        digest = mac.ComputeHash(envelope, 0, envelope.Length - 32);
                    int difference = 0;
                    for (int i = 0; i < 32; i++)
                        difference |= digest[i] ^ envelope[envelope.Length - 32 + i];
                    if (difference != 0)
                        throw new CryptographicException("Credential authentication failed.");
                    var iv = new byte[16];
                    Buffer.BlockCopy(envelope, 17, iv, 0, 16);
                    aes.Key = key;
                    aes.IV = iv;
                    byte[] clear;
                    using (var decryptor = aes.CreateDecryptor())
                        clear = decryptor.TransformFinalBlock(envelope, 33, envelope.Length - 65);
                    try
                    {
                        return Encoding.UTF8.GetString(clear);
                    }
                    finally
                    {
                        Array.Clear(clear, 0, clear.Length);
                    }
                }
                finally
                {
                    Array.Clear(key, 0, key.Length);
                    Array.Clear(macKey, 0, macKey.Length);
                }
            }
        }
    }
}
