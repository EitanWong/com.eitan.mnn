using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNKokoroModel
    {
        internal static bool IsSherpa(string directory) => File.Exists(Path.Combine(directory, "model.mnn")) &&
            File.Exists(Path.Combine(directory, "voices.bin")) && File.Exists(Path.Combine(directory, "tokens.txt"));
        internal static bool IsComplete(string directory) => MNNKokoro.IsComplete(directory) || IsSherpa(directory) &&
            File.Exists(Path.Combine(directory, "lexicon-zh.txt")) && File.Exists(Path.Combine(directory, "lexicon-us-en.txt"));

        internal static string[] ConstantVoiceNames(string directory)
        {
            string path = Path.Combine(directory, "tts_config.json");
            if (!File.Exists(path) || !File.Exists(Path.Combine(directory, "voices.mnn")))
                return Array.Empty<string>();
            var names = MNNModelData.Array(MNNModelData.Get(MNNModelData.Read(path), "styles")).Select(value => value as string).ToArray();
            if (names.Any(name => string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[]{'/', '\\'}) >= 0) || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
                throw new InvalidDataException("Invalid Kokoro constant voice names.");
            if (names.Length == 0)
            {
                // This published export omitted styles. Its single Const payload
                // matches the independently checked zf_xiaoxiao table byte for byte.
                // Recognize only that immutable graph; never invent a voice for an
                // arbitrary empty style list or edit the original download.
                using (var stream = File.OpenRead(Path.Combine(directory, "voices.mnn")))
                using (var hash = SHA256.Create())
                    if (stream.Length == 522476 && string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""),
                        MNNVerifiedTtsModel.KokoroSingleVoicesHash, StringComparison.OrdinalIgnoreCase))
                        return new[]{"zf_xiaoxiao"};
            }
            return names;
        }

        internal static float[] ConstantVoice(string directory, int index, int voices)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, "voices.mnn"));
            var data = new MNNSerializedGraph.Reader(bytes);
            int ops = data.Follow(data.Field(data.Int(0), 3));
            if (data.Count(ops) != voices || index < 0 || index >= voices)
                throw new InvalidDataException("Kokoro constant voice count does not match styles.");
            int op = data.Follow(ops + 4 + index * 4);
            if (data.Int(data.Field(op, 5)) != 11)
                throw new InvalidDataException("Expected a Kokoro Const voice table.");
            int blob = data.Follow(data.Field(op, 2));
            int[] dimensions = data.Integers(data.Field(blob, 0));
            int values = data.Follow(data.Field(blob, 7));
            if (dimensions.Length != 3 || dimensions[0] < 510 || dimensions[0] > 512 || dimensions[1] != 1 || dimensions[2] != 256 ||
                data.Int(data.Field(blob, 2)) != 1 || data.Count(values) != dimensions[0] * 256)
                throw new InvalidDataException("Unexpected Kokoro constant voice shape/type.");
            float[] result = new float[510 * 256];
            Buffer.BlockCopy(bytes, values + 4, result, 0, result.Length * 4);
            return result;
        }

        // ExtraInfo.buffer is itself an official serialized Extra table containing
        // Attribute strings. Read metadata with seeks instead of loading 350 MB
        // every time Studio builds its installed-model list.
        internal static string[] SherpaVoices(string directory)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, "model.mnn")))
            using (var reader = new BinaryReader(stream))
            {
                int Read(int p)
                {
                    if (p < 0 || p > stream.Length - 4)
                        throw new InvalidDataException("Truncated Kokoro graph metadata.");
                    stream.Position = p;
                    return reader.ReadInt32();
                }
                int Field(int table, int slot)
                {
                    int vtable = checked(table - Read(table));
                    stream.Position = vtable;
                    ushort length = reader.ReadUInt16();
                    if (4 + slot * 2 >= length)
                        throw new InvalidDataException("Missing Kokoro graph metadata.");
                    stream.Position = vtable + 4 + slot * 2;
                    int offset = reader.ReadUInt16();
                    if (offset == 0)
                        throw new InvalidDataException("Missing Kokoro graph metadata.");
                    return checked(table + offset);
                }
                int Follow(int p) => checked(p + Read(p));
                int extra = Follow(Field(Read(0), 2));
                int buffer = Follow(Field(extra, 0)), count = Read(buffer);
                if (count < 4 || count > 1024 * 1024 || buffer + 4L + count > stream.Length)
                    throw new InvalidDataException("Invalid Kokoro metadata buffer.");
                stream.Position = buffer + 4;
                byte[] bytes = reader.ReadBytes(count);
                var data = new MNNSerializedGraph.Reader(bytes);
                int root = data.Int(0), attributes = data.Follow(data.Field(root, 3));
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < data.Count(attributes); ++i)
                {
                    int attribute = data.Follow(attributes + 4 + i * 4);
                    values.Add(data.Text(data.Follow(data.Field(attribute, 3))), data.Text(data.Follow(data.Field(attribute, 0))));
                }
                if (!values.TryGetValue("model_type", out string type) || type != "kokoro" ||
                    !values.TryGetValue("sample_rate", out string rate) || rate != "24000" ||
                    !values.TryGetValue("style_dim", out string dimensions) || dimensions != "510,1,256" ||
                    !values.TryGetValue("speaker_names", out string names) ||
                    !values.TryGetValue("n_speakers", out string speakers) || !int.TryParse(speakers, out int speakerCount))
                    throw new InvalidDataException("Unsupported Kokoro metadata contract.");
                string[] voices = names.Split(',');
                bool knownExpanded = false;
                if (voices.Length == 103 && speakerCount == 156 && stream.Length == 1089920)
                {
                    stream.Position = 0;
                    using (var hash = SHA256.Create())
                        knownExpanded = string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""),
                            "0b826a32e280a87f4d061c6d1530080887b768224657c3c216764ad1326d89e6", StringComparison.OrdinalIgnoreCase);
                }
                // This exact export appends 53 unnamed tables but leaves the
                // original 103 names/IDs intact. Expose only that named prefix.
                if ((!knownExpanded && voices.Length != speakerCount) || voices.Distinct(StringComparer.Ordinal).Count() != voices.Length ||
                    voices.Any(string.IsNullOrWhiteSpace) || new FileInfo(Path.Combine(directory, "voices.bin")).Length != speakerCount * 510L * 256 * 4)
                    throw new InvalidDataException("Kokoro speaker metadata and voice table do not match.");
                return voices;
            }
        }
    }
}
