using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Unity
{
    public sealed class MNNMemorySecretStore : IMNNSecretStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public string Protection => "Session memory only; no system vault available";
        public bool CanPersist => false;
        public string Read(string alias)
        {
            MNNSecretStore.AliasHash(alias);
            return values.TryGetValue(alias, out var value) ? value : null;
        }

        public void Write(string alias, string secret)
        {
            MNNSecretStore.AliasHash(alias);
            if (secret == null || secret.Length > 16384)
                throw new ArgumentException("Invalid credential.");
            values[alias] = secret;
        }

        public void Delete(string alias) => values.Remove(alias);
    }
}
