using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Atomic, size-bounded nonsecret JSON preferences, usable by Inspector or a runtime settings UI.</summary>
    public sealed class MNNJsonSettingsStore<T> where T : class
    {
        private readonly string _path;
        private readonly Action<T> _validate;
        private readonly int _maximumBytes;
        public MNNJsonSettingsStore(string path, Action<T> validate, int maximumBytes = 1024 * 1024)
        {
            if (maximumBytes < 1 || maximumBytes > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
            _maximumBytes = maximumBytes;
        }
        public T Load()
        {
            if (!File.Exists(_path)) return null;
            if (new FileInfo(_path).Length > _maximumBytes) throw new InvalidDataException("Settings exceeded their size budget.");
            var settings = JsonUtility.FromJson<T>(File.ReadAllText(_path, Encoding.UTF8));
            if (settings == null) throw new InvalidDataException("Invalid settings document.");
            _validate(settings);
            return settings;
        }
        public void Save(T settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _validate(settings);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(settings, true));
            if (bytes.Length > _maximumBytes) throw new InvalidDataException("Settings exceeded their size budget.");
            MNNSecretStore.WriteAtomic(_path, bytes);
        }
    }
}
