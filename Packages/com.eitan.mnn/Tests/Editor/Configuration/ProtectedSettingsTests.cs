using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ProtectedSettingsTests
    {
        [Serializable] public sealed class Preferences { public int version = 1; public string model; }
        [Test]
        public void ProtectedEnvelopeDetectsTamperingAndWrongPassword()
        {
            var envelope = MNNProtectedSecrets.Encrypt("test-only-secret", "correct test password");
            Assert.AreEqual("test-only-secret", MNNProtectedSecrets.Decrypt(envelope, "correct test password"));
            Assert.Throws<CryptographicException>(() => MNNProtectedSecrets.Decrypt(envelope, "wrong test password"));
            envelope[33] ^= 1;
            Assert.Throws<CryptographicException>(() => MNNProtectedSecrets.Decrypt(envelope, "correct test password"));
        }
        [Test]
        public void SettingsValidationRejectsCorruptionAndAtomicSaveReplacesPriorDocument()
        {
            string directory = Path.GetFullPath("TestArtifacts~/SiriAI/Backend/Settings-" + Guid.NewGuid().ToString("N"));
            try
            {
                var path = Path.Combine(directory, "preferences.json");
                var store = new MNNJsonSettingsStore<Preferences>(path, p => { if (p.version != 1) throw new NotSupportedException(); });
                Assert.IsNull(store.Load());
                store.Save(new Preferences { model = "first" });
                store.Save(new Preferences { model = "second" });
                Assert.AreEqual("second", store.Load().model);
                Assert.AreEqual(1, Directory.GetFiles(directory).Length);
                Assert.Throws<NotSupportedException>(() => store.Save(new Preferences { version = 2 }));
                Assert.AreEqual("second", store.Load().model);
                File.WriteAllText(path, "{\"version\":2}");
                Assert.Throws<NotSupportedException>(() => store.Load());
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
