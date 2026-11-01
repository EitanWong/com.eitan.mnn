using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNDtlnAecModel
    {
        internal const string ConfigName = "mnn_dtln_aec.json";
        internal static readonly int[] Variants = {128, 256, 512};
        internal static string Repository(int units) => "EitanWang/DTLN-aec-" + units + "-MNN";
        internal static string Graph(int units, int stage) => "dtln_aec_" + units + "_" + stage + ".mnn";
        internal static bool IsRepository(string repository)
        {
            foreach (int units in Variants)
                if (string.Equals(repository, Repository(units), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        internal static int Units(string repository)
        {
            foreach (int units in Variants)
                if (string.Equals(repository, Repository(units), StringComparison.OrdinalIgnoreCase)) return units;
            throw new ArgumentException("Unknown DTLN AEC repository.", nameof(repository));
        }
        internal static string Revision(int units)
        {
            switch (units)
            {
                case 128: return "b7ab06ea41439f084d146319e876d49e6413df91";
                case 256: return "5c9ce2050ec09f2095ec74bd6c7c28ba419b573e";
                case 512: return "d676b5e74cf9a7515672da4acad274fbd12d3e5a";
                default: throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
        internal static long[] Sizes(int units)
        {
            switch (units)
            {
                case 128: return new long[] {2016928, 6351756};
                case 256: return new long[] {5572768, 11082744};
                case 512: return new long[] {17403040, 25263088};
                default: throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
        internal static string[] Hashes(int units)
        {
            switch (units)
            {
                case 128: return new[] {"8ac575ba4ab8e6401cdc55ccf70eedbb0bcf1950247c52e60c1176a7e60d878d", "8cffe1498d5a635b23f7124d63f88b885758915f372784e5f86c597a8a201cc1"};
                case 256: return new[] {"fa651ecf5b900b09b129ae9888965ca197fd79cad6b139ccd7d5155475cbdca5", "fde25baca0b7dd496716aafcc60111e0b176470cb93a30e59536797cdd4fe9d9"};
                case 512: return new[] {"e55420eeccda28aa76c7f06b52de5e829c7c902aee2e891f79ef7e5efcaa7102", "ef34d8ace40f07acf0ea0963348e37a390b5361f459778acda9c50cacd9c622c"};
                default: throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
        internal const string SourceRevision = "9d24e128b4f409db18227b8babb343016625921f";
        internal static string GpuFront(int units, int stage) => "dtln_aec_" + units + "_" + stage + "_front.mnn";
        internal static string GpuCore(int units, int stage) => "dtln_aec_" + units + "_" + stage + "_core.mnn";
        private static string[] GpuFiles(int units) => new[] { GpuFront(units, 1), GpuCore(units, 1), GpuFront(units, 2), GpuCore(units, 2) };
        private static long[] GpuSizes(int units)
        {
            switch (units)
            {
                case 128: return new long[] { 12464, 2004716, 2119624, 4232628 };
                case 256: return new long[] { 12464, 5560556, 2119624, 8963616 };
                case 512: return new long[] { 12464, 17390828, 2119624, 23143960 };
                default: throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
        private static string[] GpuHashes(int units)
        {
            switch (units)
            {
                case 128: return new string[] { "48ec58b811e700299330835b156ad48e03776fae56aa9e8ed3f758b34fa848d5", "0469bf304d2d4b4de85ce229c24eb32bb8d5360ee190a93affd547cff92e4f24", "1f2a94c535b754bb2c129a76ce8100da95f7672d24717e8730874a641c5afb99", "b5421d00b9edf4661371ee5cf9a6343f0a6a3363eb25d9521a86f83b8c4c3908" };
                case 256: return new string[] { "301a978a5e898179ab95d9376c7e9b03604a85a0c61cec2a41f1a553fe080e1f", "ab7babbf957604bb38884685e643d80b148ff7756585c0a6fbaee4477d3d4c61", "728ccd3e508254b8d73fb4bafe5fa08f35ab3f372c2fdddc4ccf7c2b2917a8bc", "4f5d6dfc82b41f6901cf51b66391d0ba92d7b840fbc5e41ddbd0b620444b446d" };
                case 512: return new string[] { "e5c504a3bc9cbf443dcbe6f7d1000a79e0510e715b0ec1acec5cb217a5743685", "2194da2b2118576336fdf23bce451938e519422910280a6bd67d014f57bb39e5", "88d26974c7035dc6061a980d5c78b64e9edefcd89c7a8d9284e84f20f3257505", "7b2c58d96a87f5cb510cda7263c7f3f93334e766fbf58e68eb998d54c1e87b27" };
                default: throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
        internal static void VerifyGpu(string directory, int units)
        {
            var files = GpuFiles(units); var sizes = GpuSizes(units); var hashes = GpuHashes(units);
            for (int i = 0; i < files.Length; ++i) VerifyGraph(directory, files[i], sizes[i], hashes[i]);
        }
        private static void VerifyGraph(string directory, string file, long bytes, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, file)))
            using (var sha = SHA256.Create())
                if (stream.Length != bytes || !string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("DTLN AEC graph failed SHA-256 verification: " + file);
        }
        internal static void Verify(string directory, int units)
        {
            var sizes = Sizes(units);
            var hashes = Hashes(units);
            for (int i = 0; i < 2; ++i) VerifyGraph(directory, Graph(units, i + 1), sizes[i], hashes[i]);
        }
        internal static void Prepare(string directory, string repository)
        {
            int units = Units(repository);
            Verify(directory, units);
            if (File.Exists(Path.Combine(directory, ConfigName)) && MNNDtlnAecConfig.Read(directory).gpuSplit)
            {
                if (MNNDtlnAecConfig.Read(directory).units != units) throw new InvalidDataException("DTLN AEC installed profile variant does not match the requested repository.");
                VerifyGpu(directory, units);
                return;
            }
            File.WriteAllText(Path.Combine(directory, ConfigName), JsonUtility.ToJson(new MNNDtlnAecConfig {repository = Repository(units), units = units, revision = Revision(units)}, true));
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNDtlnAecConfig.Read(directory);
                var sizes = Sizes(config.units);
                for (int i = 0; i < 2; ++i)
                    if (new FileInfo(Path.Combine(directory, Graph(config.units, i + 1))).Length != sizes[i]) return false;
                if (config.gpuSplit)
                {
                    var files = GpuFiles(config.units); var gpuSizes = GpuSizes(config.units);
                    for (int i = 0; i < files.Length; ++i)
                        if (new FileInfo(Path.Combine(directory, files[i])).Length != gpuSizes[i]) return false;
                }
                return true;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
    }
}
