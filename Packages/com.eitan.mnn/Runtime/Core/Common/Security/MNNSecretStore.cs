using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Application credentials. Unsupported systems default to memory, never plaintext PlayerPrefs.</summary>
    public static class MNNSecretStore
    {
        public static IMNNSecretStore Create(string applicationId, string directory)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
                throw new ArgumentException(nameof(applicationId));
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return new MNNAppleSecretStore(applicationId);
#elif UNITY_IOS && !UNITY_EDITOR
            return new MNNAppleSecretStore(applicationId);
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return new MNNWindowsSecretStore(directory);
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new MNNAndroidSecretStore(applicationId, directory);
#else
            return new MNNMemorySecretStore();
#endif
        }

        public static string AliasHash(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias) || alias.Length > 256)
                throw new ArgumentException("Invalid credential alias.");
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(alias))).Replace("-", "").ToLowerInvariant();
        }

        public static void WriteAtomic(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
