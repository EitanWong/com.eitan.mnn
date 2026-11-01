using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Portable opt-in vault. The application must request its passphrase at each launch, never bundle it.</summary>
    public sealed class MNNPasswordSecretStore : IMNNSecretStore
    {
        private readonly string directory;
        private readonly string passphrase;
        public string Protection => "AES-256-CBC + HMAC-SHA256 / PBKDF2-SHA256; user passphrase required";
        public bool CanPersist => true;
        public MNNPasswordSecretStore(string directory, string passphrase)
        {
            if (string.IsNullOrEmpty(passphrase) || passphrase.Length < 12)
                throw new ArgumentException("Use at least 12 passphrase characters.");
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
            this.passphrase = passphrase;
        }

        private string PathFor(string alias) => Path.Combine(directory, MNNSecretStore.AliasHash(alias) + ".secret");
        public string Read(string alias) => File.Exists(PathFor(alias)) ? MNNProtectedSecrets.Decrypt(File.ReadAllBytes(PathFor(alias)), passphrase + "\n" + alias) : null;
        public void Write(string alias, string secret) => MNNSecretStore.WriteAtomic(PathFor(alias), MNNProtectedSecrets.Encrypt(secret, passphrase + "\n" + alias));
        public void Delete(string alias)
        {
            if (File.Exists(PathFor(alias)))
                File.Delete(PathFor(alias));
        }
    }
}
