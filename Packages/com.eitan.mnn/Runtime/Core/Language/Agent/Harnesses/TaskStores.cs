using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MNN.Samples.SiriAI.Harnesses
{
    public interface IHarnessTaskStore
    {
        HarnessTask[] Load();
        void Save(HarnessTask[] tasks);
    }

    public sealed class MemoryTaskStore : IHarnessTaskStore
    {
        private HarnessTask[] _tasks = new HarnessTask[0];
        public bool FailWrites { get; set; }
        public HarnessTask[] Load() => _tasks.Select(task => task.Copy()).ToArray();
        public void Save(HarnessTask[] tasks)
        {
            if (FailWrites) throw new IOException("Task store write failed.");
            _tasks = tasks.Select(task => task.Copy()).ToArray();
        }
    }

    /// <summary>One process holds the writer lease. File replacement commits tasks and outbox together.</summary>
    public sealed class FileTaskStore : IHarnessTaskStore, IDisposable
    {
        private readonly string _path;
        private readonly FileStream _writerLease;
        private bool _disposed;
        public FileTaskStore(string directory)
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(Path.GetFullPath(directory), "tasks.json");
            _writerLease = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        public HarnessTask[] Load()
        {
            RequireOpen();
            if (!File.Exists(_path)) return new HarnessTask[0];
            if (new FileInfo(_path).Length > 64 * 1024 * 1024) throw new SerializationException("Task store exceeds its budget.");
            using (var input = File.OpenRead(_path))
            {
                var data = (StoreDocument)new DataContractJsonSerializer(typeof(StoreDocument)).ReadObject(input);
                if (data == null || data.Version != 1 || data.Tasks == null) throw new SerializationException("Unsupported task store schema.");
                return data.Tasks;
            }
        }
        public void Save(HarnessTask[] tasks)
        {
            RequireOpen();
            string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(StoreDocument)).WriteObject(output, new StoreDocument { Version = 1, Tasks = tasks });
                    output.Flush(true);
                }
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private void RequireOpen() { if (_disposed) throw new ObjectDisposedException(nameof(FileTaskStore)); }
        public void Dispose() { if (_disposed) return; _disposed = true; _writerLease.Dispose(); }

        [DataContract]
        private sealed class StoreDocument
        {
            [DataMember] public int Version;
            [DataMember] public HarnessTask[] Tasks;
        }
    }
}
