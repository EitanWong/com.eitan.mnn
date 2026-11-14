using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    public sealed partial class MNNConversationTranscriptStore
    {
        private bool _indexDirty = true;
        /// <summary>True when a matching disposable index avoided decoding sealed history on opening.
        /// Sealed records still validate their journal checksum when read. Missing, stale or damaged indexes
        /// fall back to journal recovery; the journal remains authoritative.</summary>
        public bool RecoveryIndexUsed { get; private set; }

        private bool TryRestoreIndex()
        {
            string path = _path + ".idx";
            if (!File.Exists(path)) return false;
            try
            {
                var info = new FileInfo(path);
                if (info.Length < 72 || info.Length > 72 + (_maximumBytes / (BlockSize * 10)) * 8) return false;
                byte[] bytes = File.ReadAllBytes(path);
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(bytes, 0, bytes.Length - 32);
                    for (int i = 0; i < hash.Length; ++i) if (hash[i] != bytes[bytes.Length - 32 + i]) return false;
                }
                using (var reader = new BinaryReader(new MemoryStream(bytes, 0, bytes.Length - 32)))
                {
                    if (reader.ReadInt64() != 0x315844494E4E4D || reader.ReadInt64() != _stream.Length ||
                        reader.ReadInt64() != File.GetLastWriteTimeUtc(_path).Ticks) return false;
                    int count = reader.ReadInt32(); long tail = reader.ReadInt64(); int blocks = reader.ReadInt32();
                    if (count < 0 || blocks != Math.Max(0, (count - 1) / BlockSize) || bytes.Length != 72L + blocks * 8L ||
                        tail < HeaderBytes || tail > _stream.Length || _stream.Length > _maximumBytes) return false;
                    _stream.Position = 0;
                    var header = new byte[HeaderBytes];
                    if (!ReadExact(header, header.Length) || !AcceptHeader(header)) return false;
                    long previous = HeaderBytes - 1;
                    for (int i = 0; i < blocks; ++i)
                    {
                        long offset = reader.ReadInt64();
                        if (offset <= previous || offset >= tail || (i == 0 && offset != HeaderBytes)) return false;
                        _offsets.Add(offset); previous = offset;
                    }
                    if (blocks == 0 && tail != HeaderBytes) return false;
                    _stream.Position = tail;
                    while (_stream.Position < _stream.Length)
                    {
                        if (!TryReadRecord(_stream.Length, out byte operation, out var entry)) return false;
                        if (operation == 1)
                        { if (_tail.Count == BlockSize) return false; _tail.Add(entry); }
                        else
                        {
                            if (_tail.Count == 0 || entry.Role != _tail[_tail.Count - 1].Role) return false;
                            _tail[_tail.Count - 1] = entry;
                        }
                    }
                    if (count != blocks * BlockSize + _tail.Count) return false;
                    Count = count; _tailStart = tail; _appendOffset = _stream.Length;
                    _indexDirty = false; RecoveryIndexUsed = true; return true;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            finally
            {
                if (!RecoveryIndexUsed)
                { _offsets.Clear(); _tail.Clear(); Count = 0; _tailStart = _appendOffset = HeaderBytes; _stream.Position = 0; }
            }
        }

        private void SaveIndex()
        {
            if (!_indexDirty) return;
            string path = _path + ".idx", temporary = path + ".tmp";
            try
            {
                using (var memory = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(memory, Utf8, true))
                    {
                        writer.Write(0x315844494E4E4DL); writer.Write(_stream.Length);
                        writer.Write(File.GetLastWriteTimeUtc(_path).Ticks); writer.Write(Count);
                        writer.Write(_tailStart); writer.Write(_offsets.Count);
                        foreach (long offset in _offsets) writer.Write(offset);
                    }
                    byte[] bytes = memory.ToArray();
                    using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                    using (var sha = SHA256.Create())
                    {
                        output.Write(bytes, 0, bytes.Length); byte[] hash = sha.ComputeHash(bytes);
                        output.Write(hash, 0, hash.Length); output.Flush(true);
                    }
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                _indexDirty = false;
            }
            // An optional performance cache cannot turn a durable journal checkpoint into a failure.
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
