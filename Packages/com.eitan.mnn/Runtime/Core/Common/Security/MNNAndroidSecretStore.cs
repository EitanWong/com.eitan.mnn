#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Android API 23+ AES/GCM keys remain inside AndroidKeyStore. Call on Unity's main thread.</summary>
    internal sealed class MNNAndroidSecretStore : IMNNSecretStore
    {
        private readonly string service;
        private readonly string directory;
        public string Protection => "AndroidKeyStore AES/GCM (hardware backing depends on device)";
        public bool CanPersist => true;
        public MNNAndroidSecretStore(string service, string directory)
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                if (version.GetStatic<int>("SDK_INT") < 23) throw new PlatformNotSupportedException("AndroidKeyStore AES/GCM requires Android API 23+.");
            this.service = service; this.directory = directory;
        }
        private string PathFor(string alias) => Path.Combine(directory, MNNSecretStore.AliasHash(alias) + ".keystore");
        private AndroidJavaObject Key(string alias)
        {
            using (var cls = new AndroidJavaClass("java.security.KeyStore"))
            using (var store = cls.CallStatic<AndroidJavaObject>("getInstance", "AndroidKeyStore"))
            {
                store.Call("load", null, null);
                string name = service + "." + MNNSecretStore.AliasHash(alias);
                if (store.Call<bool>("containsAlias", name)) return store.Call<AndroidJavaObject>("getKey", name, null);
                using (var builder = new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder", name, 3))
                using (var modes = builder.Call<AndroidJavaObject>("setBlockModes", new object[] { new string[] { "GCM" } }))
                using (var padding = builder.Call<AndroidJavaObject>("setEncryptionPaddings", new object[] { new string[] { "NoPadding" } }))
                using (var spec = builder.Call<AndroidJavaObject>("build"))
                using (var generatorClass = new AndroidJavaClass("javax.crypto.KeyGenerator"))
                using (var generator = generatorClass.CallStatic<AndroidJavaObject>("getInstance", "AES", "AndroidKeyStore"))
                {
                    generator.Call("init", spec);
                    return generator.Call<AndroidJavaObject>("generateKey");
                }
            }
        }
        public void Write(string alias, string secret)
        {
            if (secret == null || secret.Length > 16384) throw new ArgumentException("Invalid credential.");
            using (var key = Key(alias))
            using (var cls = new AndroidJavaClass("javax.crypto.Cipher"))
            using (var cipher = cls.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding"))
            {
                cipher.Call("init", 1, key);
                byte[] clear = Encoding.UTF8.GetBytes(secret), encrypted;
                try { encrypted = cipher.Call<byte[]>("doFinal", clear); }
                finally { Array.Clear(clear, 0, clear.Length); }
                byte[] iv = cipher.Call<byte[]>("getIV");
                if (iv.Length != 12) throw new InvalidOperationException("Unexpected Android GCM nonce size.");
                var envelope = new byte[12 + encrypted.Length];
                Buffer.BlockCopy(iv, 0, envelope, 0, 12);
                Buffer.BlockCopy(encrypted, 0, envelope, 12, encrypted.Length);
                MNNSecretStore.WriteAtomic(PathFor(alias), envelope);
            }
        }
        public string Read(string alias)
        {
            if (!File.Exists(PathFor(alias))) return null;
            byte[] envelope = File.ReadAllBytes(PathFor(alias));
            if (envelope.Length < 28 || envelope.Length > 131072) throw new InvalidOperationException("Invalid Android credential.");
            var iv = new byte[12];
            Buffer.BlockCopy(envelope, 0, iv, 0, 12);
            using (var key = Key(alias))
            using (var cls = new AndroidJavaClass("javax.crypto.Cipher"))
            using (var cipher = cls.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding"))
            using (var spec = new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec", 128, iv))
            {
                cipher.Call("init", 2, key, spec);
                var encrypted = new byte[envelope.Length - 12];
                Buffer.BlockCopy(envelope, 12, encrypted, 0, encrypted.Length);
                byte[] clear = cipher.Call<byte[]>("doFinal", encrypted);
                try { return Encoding.UTF8.GetString(clear); }
                finally { Array.Clear(clear, 0, clear.Length); }
            }
        }
        public void Delete(string alias) { if (File.Exists(PathFor(alias))) File.Delete(PathFor(alias)); }
    }
}
#endif
