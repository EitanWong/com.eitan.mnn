using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Prepared by the export's voice extractor, never inferred from a raw WAV.
    internal sealed class MNNQwen3TtsVoice
    {
        internal readonly int[] TextIds, Codes;
        internal readonly float[] Speaker;

        private MNNQwen3TtsVoice(int[] textIds, int[] codes, float[] speaker)
        {
            TextIds = textIds;
            Codes = codes;
            Speaker = speaker;
        }

        internal static MNNQwen3TtsVoice Read(string directory)
        {
            string metadataPath = Path.Combine(directory, "meta.json");
            if (new FileInfo(metadataPath).Length > 65536)
                throw new InvalidDataException("Qwen voice metadata exceeds 64 KiB.");
            var metadata = MNNModelData.Read(metadataPath);
            var ids = MNNModelData.Array(MNNModelData.Get(metadata, "ref_ids"));
            if (ids.Count == 1 && ids[0] is System.Collections.Generic.List<object>)
                ids = MNNModelData.Array(ids[0]);
            int[] textIds = ids.Select(MNNModelData.Integer).ToArray();
            if (textIds.Length < 6 || textIds.Length > 4096 || textIds.Any(n => n < 0 || n >= 151936) ||
                !textIds.Take(3).SequenceEqual(new[]{151644, 77091, 198}) ||
                !textIds.Skip(textIds.Length - 2).SequenceEqual(new[]{151645, 198}))
                throw new InvalidDataException("Invalid Qwen reference token IDs or assistant template.");
            int frames = MNNModelData.Integer(MNNModelData.Get(metadata, "ref_frames"));
            if (frames < 1 || frames > 360)
                throw new InvalidDataException("Invalid Qwen reference duration.");
            string codePath = Path.Combine(directory, "ref_code.i32.bin");
            string speakerPath = Path.Combine(directory, "spk_emb.f32.bin");
            if (new FileInfo(codePath).Length != frames * 16 * 4 || new FileInfo(speakerPath).Length != 4096)
                throw new InvalidDataException("Invalid Qwen reference asset sizes.");
            var codes = new int[frames * 16];
            var speaker = new float[1024];
            Buffer.BlockCopy(File.ReadAllBytes(codePath), 0, codes, 0, codes.Length * 4);
            Buffer.BlockCopy(File.ReadAllBytes(speakerPath), 0, speaker, 0, speaker.Length * 4);
            if (codes.Any(n => n < 0 || n >= 2048) || speaker.Any(n => float.IsNaN(n) || float.IsInfinity(n)) ||
                !speaker.Any(n => n != 0))
                throw new InvalidDataException("Invalid Qwen reference values.");
            return new MNNQwen3TtsVoice(textIds, codes, speaker);
        }
    }
}
