using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNEasyTurnProfile
    {
        [Serializable] internal sealed class Config { public string source_repository, source_revision, prompt; }
        internal static Config Read(string directory)
        {
            var c = JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(directory, MNNEasyTurn.ConfigFileName)));
            if (c == null || c.source_repository != "ASLP-lab/Easy-Turn" || c.source_revision != "7d1f8b06776b575ac22e2fd4aa7913da2f4b8b98" || string.IsNullOrWhiteSpace(c.prompt))
                throw new InvalidDataException("Unsupported Easy-Turn deployment.");
            return c;
        }
        internal static void Verify(string directory)
        {
            Read(directory);
            var files = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "files.sha256.json")));
            if (new[] { "audio.mnn", "decoder.mnn", "embeddings_bf16.bin", "speech-boundary.f32", "vocab.json", "merges.txt", "added_tokens.json" }.Any(n => !files.ContainsKey(n)))
                throw new InvalidDataException("Incomplete Easy-Turn deployment manifest.");
            foreach (var pair in files)
            {
                if (Path.GetFileName(pair.Key) != pair.Key) throw new InvalidDataException("Expected sibling Easy-Turn files.");
                string path = Path.Combine(directory, pair.Key);
                if (!File.Exists(path) || new FileInfo(path).Length != Convert.ToInt64(MNNModelData.Get(pair.Value, "bytes")) ||
                    MNNModelDeliveryManifest.ComputeSha256(path) != (string)MNNModelData.Get(pair.Value, "sha256"))
                    throw new InvalidDataException("Easy-Turn checksum mismatch: " + pair.Key);
            }
            if (new FileInfo(Path.Combine(directory, "speech-boundary.f32")).Length != 1792 * 4 ||
                new FileInfo(Path.Combine(directory, "embeddings_bf16.bin")).Length != 151936L * 896 * 2)
                throw new InvalidDataException("Invalid Easy-Turn embedding dimensions.");
        }
        internal static bool IsComplete(string directory)
        {
            try { Verify(directory); return true; }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return false; }
        }
    }
}
