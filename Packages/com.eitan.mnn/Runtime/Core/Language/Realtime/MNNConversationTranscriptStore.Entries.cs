using System;
using System.IO;
using System.Text;

namespace MNN.Unity
{
    public sealed partial class MNNConversationTranscriptStore
    {
        private int EncodeEntry(byte operation, MNNConversationTranscriptEntry entry)
        {
            if (entry.Kind == MNNConversationEntryKind.Message && entry.Id.Length == 0 && entry.ParentId.Length == 0 &&
                entry.Title.Length == 0 && entry.State == MNNConversationEntryState.Completed) return Encode(operation, entry.Role, entry.Text);
            ValidateText(entry.Role, entry.Text);
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, Utf8, true))
                {
                    writer.Write((byte)(operation + 2)); writer.Write((byte)entry.Role);
                    writer.Write((byte)entry.Kind); writer.Write((byte)entry.State);
                    writer.Write(entry.Id); writer.Write(entry.ParentId); writer.Write(entry.Title); writer.Write(entry.Text);
                }
                int length = checked((int)memory.Length); EnsureRecordCapacity(length);
                Buffer.BlockCopy(memory.GetBuffer(), 0, _recordBytes, 0, length);
                return length;
            }
        }
        private bool TryDecodeEntry(int length, out byte operation, out MNNConversationTranscriptEntry entry)
        {
            operation = _recordBytes[0]; entry = default;
            try
            {
                if (operation <= 2)
                    entry = new MNNConversationTranscriptEntry((MNNChatRole)_recordBytes[1], Utf8.GetString(_recordBytes, 2, length - 2));
                else
                {
                    using (var reader = new BinaryReader(new MemoryStream(_recordBytes, 2, length - 2), Utf8))
                    {
                        var kind = (MNNConversationEntryKind)reader.ReadByte(); var state = (MNNConversationEntryState)reader.ReadByte();
                        string id = reader.ReadString(), parent = reader.ReadString(), title = reader.ReadString(), text = reader.ReadString();
                        if (reader.BaseStream.Position != reader.BaseStream.Length) return false;
                        entry = new MNNConversationTranscriptEntry((MNNChatRole)_recordBytes[1], text, kind, id, parent, title, state);
                    }
                    operation -= 2;
                }
                return entry.Text.Length <= MNNConversationMailbox.MaximumTextCharacters;
            }
            catch (ArgumentException) { return false; }
            catch (IOException) { return false; }
            catch (FormatException) { return false; }
        }
    }
}
