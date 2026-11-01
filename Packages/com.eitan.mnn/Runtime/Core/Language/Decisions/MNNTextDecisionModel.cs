using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNTextDecisionModel
    {
        internal const string Repository = "yunfengwang/laya-MNN-fp16";
        internal const string Revision = "fe3f08ab29bb100f5f78bc7ea7901898a5aaea48";
        internal static readonly string[] Files = {"laya-fp16.mnn", "tokenizer.json", "manifest.json"};
        internal static readonly long[] Sizes = {845931204, 3583228, 1374};
        internal static readonly string[] Hashes = {
            "f22212d4dc722bc600771042fcf362ad2b384485c6464c75d35b17f2ab9257e3",
            "6c8aaa9a542084f2457eab775d4eeb51f92a70c0fd9de28d5edb0ddec3c08d30",
            "c7941b089361b2ff49119c2204f1cafc8bc1481a8c819ceb59e1258d7674c556"};

        internal static bool IsComplete(string directory)
        {
            for (int i = 0; i < Files.Length; ++i)
            {
                var file = new FileInfo(Path.Combine(directory, Files[i]));
                if (!file.Exists || file.Length != Sizes[i]) return false;
            }
            return true;
        }

        internal static void Verify(string directory)
        {
            for (int i = 0; i < Files.Length; ++i)
            {
                string path = Path.Combine(directory, Files[i]);
                if (!File.Exists(path) || new FileInfo(path).Length != Sizes[i])
                    throw new InvalidDataException("Missing or incomplete English Laya asset: " + Files[i]);
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != Hashes[i])
                        throw new InvalidDataException("English Laya asset hash mismatch: " + Files[i]);
            }
        }
    }
}
