using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Random access to immutable display entries without allocating message wrappers.</summary>
    public interface IMNNConversationTranscriptSource
    {
        int Count { get; }
        MNNConversationTranscriptEntry this[int index] { get; }
    }

    /// <summary>One immutable display-history entry, independent of model prompt memory.</summary>
    public readonly struct MNNConversationTranscriptEntry
    {
        public MNNChatRole Role { get; }
        public string Text { get; }
        public MNNConversationTranscriptEntry(MNNChatRole role, string text)
        {
            if (role < MNNChatRole.System || role > MNNChatRole.Assistant) throw new ArgumentOutOfRangeException(nameof(role));
            Role = role; Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }

    /// <summary>
    /// Single-owner, bounded-resident display history. Session mode spills sealed blocks and deletes its
    /// file on disposal. Persistent mode journals changes, restores the valid prefix after a torn write,
    /// and retains the file. Call Flush for a durable checkpoint; writes alone are not durability promises.
    /// </summary>
    public sealed class MNNConversationTranscriptStore : IDisposable, IMNNConversationTranscriptSource
    {
        public const int BlockSize = 64;
        public const long DefaultMaximumBytes = 256L * 1024 * 1024;
        private const int ReadCacheSize = 2;
        private const int HeaderBytes = 8;
        private static readonly byte[] Header = { 77, 78, 78, 84, 1, 0, 0, 0 };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly int _owner = Thread.CurrentThread.ManagedThreadId;
        private readonly string _directory;
        private readonly bool _persistent;
        private readonly long _maximumBytes;
        private readonly List<long> _offsets = new List<long>();
        private readonly List<MNNConversationTranscriptEntry> _tail = new List<MNNConversationTranscriptEntry>(BlockSize);
        private readonly Dictionary<int, List<MNNConversationTranscriptEntry>> _cache = new Dictionary<int, List<MNNConversationTranscriptEntry>>();
        private readonly Queue<int> _cacheOrder = new Queue<int>();
        private FileStream _stream;
        private string _path;
        private long _tailStart = HeaderBytes, _appendOffset = HeaderBytes;
        private readonly byte[] _recordHeader = new byte[8];
        private byte[] _recordBytes = new byte[256];
        private bool _disposed;
        public int Count { get; private set; }
        /// <summary>Bytes discarded from an invalid/incomplete journal suffix during opening.</summary>
        public long RecoveredBytes { get; private set; }
        public int ResidentCount
        {
            get { CheckOwner(); int count = _tail.Count; foreach (var block in _cache.Values) count += block.Count; return count; }
        }

        public MNNConversationTranscriptStore(string directory, long maximumBytes = DefaultMaximumBytes)
            : this(directory, false, maximumBytes) { }

        private MNNConversationTranscriptStore(string location, bool persistent, long maximumBytes)
        {
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Supply a transcript location.", nameof(location));
            if (maximumBytes < HeaderBytes || maximumBytes > long.MaxValue / 2) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            _maximumBytes = maximumBytes;
            _persistent = persistent;
            if (!persistent) { _directory = Path.GetFullPath(location); return; }
            _path = Path.GetFullPath(location);
            _directory = Path.GetDirectoryName(_path);
            Directory.CreateDirectory(_directory);
            _stream = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            try { Recover(); }
            catch { _stream.Dispose(); _stream = null; throw; }
        }

        /// <summary>Opens one journal writer. A corrupt header is rejected; a corrupt suffix is truncated.</summary>
        public static MNNConversationTranscriptStore OpenPersistent(string filePath, long maximumBytes = DefaultMaximumBytes) =>
            new MNNConversationTranscriptStore(filePath, true, maximumBytes);

        public MNNConversationTranscriptEntry this[int index]
        {
            get
            {
                CheckOwner();
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                int block = index / BlockSize;
                if (block == _offsets.Count) return _tail[index % BlockSize];
                if (!_cache.TryGetValue(block, out var entries))
                {
                    entries = ReadBlock(_offsets[block], block + 1 < _offsets.Count ? _offsets[block + 1] : _tailStart);
                    if (_cache.Count == ReadCacheSize) _cache.Remove(_cacheOrder.Dequeue());
                    _cache.Add(block, entries); _cacheOrder.Enqueue(block);
                }
                return entries[index % BlockSize];
            }
        }

        public void Add(MNNChatRole role, string text)
        {
            CheckOwner();
            text = text ?? "";
            if (!_persistent) ValidateText(role, text);
            if (Count == int.MaxValue) throw new InvalidOperationException("Start a new conversation before exceeding the transcript index limit.");
            long offset = 0;
            if (_persistent) offset = Append(Encode(1, role, text));
            else if (_tail.Count == BlockSize) SpillTail();
            if (_persistent && _tail.Count == BlockSize)
            { _offsets.Add(_tailStart); _tailStart = offset; _tail.Clear(); }
            _tail.Add(new MNNConversationTranscriptEntry(role, text ?? "")); ++Count;
        }

        public void UpdateLast(string text)
        {
            CheckOwner();
            if (_tail.Count == 0) throw new InvalidOperationException("No active reply to update.");
            var previous = _tail[_tail.Count - 1];
            text = text ?? "";
            if (!_persistent) ValidateText(previous.Role, text);
            if (_persistent) Append(Encode(2, previous.Role, text));
            _tail[_tail.Count - 1] = new MNNConversationTranscriptEntry(previous.Role, text ?? "");
        }

        private static int ValidateText(MNNChatRole role, string text)
        {
            if (role < MNNChatRole.System || role > MNNChatRole.Assistant) throw new ArgumentOutOfRangeException(nameof(role));
            if (text.Length > MNNConversationMailbox.MaximumTextCharacters) throw new ArgumentException("Transcript text exceeds the character limit.", nameof(text));
            try { return Utf8.GetByteCount(text); }
            catch (EncoderFallbackException error) { throw new ArgumentException("Transcript text contains invalid Unicode.", nameof(text), error); }
        }
        private int Encode(byte operation, MNNChatRole role, string text)
        {
            int length = 2 + ValidateText(role, text);
            EnsureRecordCapacity(length);
            _recordBytes[0] = operation; _recordBytes[1] = (byte)role;
            Utf8.GetBytes(text, 0, text.Length, _recordBytes, 2); return length;
        }

        private void EnsureRecordCapacity(int length)
        {
            if (_recordBytes.Length < length)
                _recordBytes = new byte[Math.Max(length, Math.Min(2 + MNNConversationMailbox.MaximumTextCharacters * 3, _recordBytes.Length * 2))];
        }

        private long Append(int length)
        {
            long offset = _appendOffset;
            if (length + 8L > _maximumBytes - offset) throw new InvalidOperationException("Transcript disk quota exceeded. Clear it or start a new conversation.");
            if (_stream.Position != offset) _stream.Position = offset;
            try { WriteRecord(length); _appendOffset += 8 + length; }
            catch { _stream.SetLength(offset); _stream.Position = offset; throw; }
            return offset;
        }

        private void SpillTail()
        {
            if (_stream == null)
            {
                Directory.CreateDirectory(_directory);
                _path = Path.Combine(_directory, "transcript-" + Guid.NewGuid().ToString("N") + ".bin");
                _stream = new FileStream(_path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
                _stream.Write(Header, 0, Header.Length);
            }
            long offset = _appendOffset;
            try
            {
                foreach (var entry in _tail) Append(Encode(1, entry.Role, entry.Text));
                // One block checkpoint, with no per-record writers or temporary byte arrays.
                _stream.Flush();
            }
            catch { _stream.SetLength(offset); _stream.Position = offset; _appendOffset = offset; throw; }
            _offsets.Add(offset); _tailStart = _appendOffset; _tail.Clear();
        }

        private void WriteRecord(int length)
        {
            Write32(_recordHeader, 0, (uint)length); Write32(_recordHeader, 4, Checksum(_recordBytes, length));
            _stream.Write(_recordHeader, 0, _recordHeader.Length); _stream.Write(_recordBytes, 0, length);
        }
        private static void Write32(byte[] buffer, int offset, uint value)
        {
            for (int i = 0; i < 4; ++i) buffer[offset + i] = (byte)(value >> (8 * i));
        }
        private static uint Read32(byte[] buffer, int offset) =>
            buffer[offset] | ((uint)buffer[offset + 1] << 8) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);
        private static uint Checksum(byte[] bytes, int length)
        {
            uint hash = 2166136261;
            for (int i = 0; i < length; ++i) hash = unchecked((hash ^ bytes[i]) * 16777619);
            return hash;
        }
        private bool ReadExact(byte[] buffer, int length)
        {
            int offset = 0;
            while (offset < length) { int read = _stream.Read(buffer, offset, length - offset); if (read == 0) return false; offset += read; }
            return true;
        }
        private bool TryReadRecord(long end, out byte operation, out MNNConversationTranscriptEntry entry)
        {
            operation = 0; entry = default;
            if (end - _stream.Position < 8 || !ReadExact(_recordHeader, 8)) return false;
            uint rawLength = Read32(_recordHeader, 0), checksum = Read32(_recordHeader, 4);
            if (rawLength < 2 || rawLength > 2 + MNNConversationMailbox.MaximumTextCharacters * 3 || end - _stream.Position < rawLength) return false;
            int length = (int)rawLength;
            EnsureRecordCapacity(length);
            if (!ReadExact(_recordBytes, length) || Checksum(_recordBytes, length) != checksum ||
                (_recordBytes[0] != 1 && _recordBytes[0] != 2) || _recordBytes[1] > (byte)MNNChatRole.Assistant) return false;
            string text;
            try { text = Utf8.GetString(_recordBytes, 2, length - 2); }
            catch (DecoderFallbackException) { return false; }
            if (text.Length > MNNConversationMailbox.MaximumTextCharacters) return false;
            operation = _recordBytes[0]; entry = new MNNConversationTranscriptEntry((MNNChatRole)_recordBytes[1], text); return true;
        }

        private void Recover()
        {
            if (_stream.Length == 0) { _stream.Write(Header, 0, Header.Length); _stream.Flush(); return; }
            if (_stream.Length > _maximumBytes) throw new InvalidOperationException("Existing transcript exceeds the disk quota.");
            var header = new byte[HeaderBytes];
            if (_stream.Read(header, 0, header.Length) != header.Length) throw new InvalidDataException("Invalid transcript header.");
            for (int i = 0; i < header.Length; ++i) if (header[i] != Header[i]) throw new InvalidDataException("Unsupported transcript format.");
            long valid = HeaderBytes, end = _stream.Length;
            while (_stream.Position < end)
            {
                long offset = _stream.Position;
                if (!TryReadRecord(end, out var operation, out var entry)) break;
                if (operation == 1)
                {
                    if (Count == int.MaxValue) break;
                    if (_tail.Count == BlockSize) { _offsets.Add(_tailStart); _tailStart = offset; _tail.Clear(); }
                    _tail.Add(entry); ++Count;
                }
                else
                {
                    if (_tail.Count == 0 || entry.Role != _tail[_tail.Count - 1].Role) break;
                    _tail[_tail.Count - 1] = entry;
                }
                valid = _stream.Position;
            }
            RecoveredBytes = end - valid;
            _stream.SetLength(valid); _stream.Position = valid; _appendOffset = valid; _stream.Flush();
        }

        private List<MNNConversationTranscriptEntry> ReadBlock(long start, long end)
        {
            _stream.Position = start;
            var entries = new List<MNNConversationTranscriptEntry>(BlockSize);
            while (_stream.Position < end)
            {
                if (!TryReadRecord(end, out var operation, out var entry)) throw new InvalidDataException("Transcript block is corrupt.");
                if (operation == 1) entries.Add(entry);
                else if (entries.Count > 0) entries[entries.Count - 1] = entry;
                else throw new InvalidDataException("Invalid transcript update.");
            }
            if (entries.Count != BlockSize) throw new InvalidDataException("Transcript block has an invalid entry count.");
            return entries;
        }

        /// <summary>Flushes journal bytes to the backing device. Session tails remain in memory.</summary>
        public void Flush() { CheckOwner(); _stream?.Flush(true); }

        public void Clear()
        {
            CheckOwner();
            if (_persistent) { _stream.SetLength(0); _stream.Position = 0; _stream.Write(Header, 0, Header.Length); _stream.Flush(); }
            else { _stream?.Dispose(); _stream = null; if (_path != null) File.Delete(_path); _path = null; }
            _offsets.Clear(); _tail.Clear(); _cache.Clear(); _cacheOrder.Clear(); Count = 0;
            _tailStart = _appendOffset = HeaderBytes; RecoveredBytes = 0;
        }

        private void CheckOwner()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNConversationTranscriptStore));
            if (Thread.CurrentThread.ManagedThreadId != _owner) throw new InvalidOperationException("Transcript history has one owner; use it from its creating thread.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            CheckOwner();
            if (!_persistent) Clear();
            _stream?.Dispose(); _stream = null; _tail.Clear(); _cache.Clear(); _offsets.Clear(); _disposed = true;
        }
    }
}
