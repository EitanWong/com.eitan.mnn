using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNPaddleTextModel
    {
        internal const string Repository = "qefro/pp-OCRv5-mnn";
        internal const string Revision = "8ddc5f25119bccd0d444d52d84d5e52648f4beca";
        internal sealed class File
        {
            internal readonly string Path, Sha256, Profile;
            internal string InstalledPath => Profile == null ? Path : System.IO.Path.Combine("recognizers", Profile, Path);
            internal readonly long Bytes;
            internal File(string path, long bytes, string sha256, string profile = null) { Path = path; Bytes = bytes; Sha256 = sha256; Profile = profile; }
        }
        internal static File[] Files() => new[]
        {
            new File("PP-OCRv5_mobile_det.mnn", 4760244, "326f846bb5c903282e116ea089e8796b67921586726cca9457730436a79684c3"),
            new File("en_PP-OCRv5_mobile_rec_infer.mnn", 3967840, "c5e747fb69275e9d99fbe54f97642c65681c3e4244383f825d1d7668e9aece81"),
            new File("ppocr_keys_en.txt", 1416, "e025a66d31f327ba0c232e03f407ae8d105e1e709e7ccb3f408aa778c24e70d6"),
            new File("PP-OCRv5_mobile_rec.mnn", 16531596, "c809800b09263a8d18c678c211e470ffc464cbb33db2e6bde0244766f3feb0db", "chinese"),
            new File("ppocr_keys_v5.txt", 74008, "f2ed6bb20a850ce4767fa9b4622d9b282985ab7f0ea8f8c11abd790ca6d2ff94", "chinese"),
            new File("latin_PP-OCRv5_mobile_rec_infer.mnn", 4065440, "f3da21186ade13e0d3553867205a8983d9fda46e10b6a15e5f6308325e6e7a55", "latin"),
            new File("ppocr_keys_latin.txt", 2616, "ccbcc45730b3fbbd9050c5bc74db6a99067141ef1035e3d14889a84a6b9b1aff", "latin"),
            new File("cyrillic_PP-OCRv5_mobile_rec_infer.mnn", 4068856, "5fe37fb8382e9cd227afa2605ba99202466960bc7aa9e22ae6b1ce05ecedf2e1", "cyrillic"),
            new File("ppocr_keys_cyrillic.txt", 2781, "db40aa52ceb112055be80c694afdf655d5d2c4f7873704524cc16a447ca913ba", "cyrillic"),
            new File("el_PP-OCRv5_mobile_rec_infer.mnn", 3947832, "302097ec28112f9be70a1edd1147f9392755c2ea4d7831cdf95d04f1bc065e2b", "greek"),
            new File("ppocr_keys_el.txt", 1103, "31defc62c0c3ad3674a82da6192226a2ba98ef4ff014a7045cb88d59f9c3de31", "greek"),
            new File("arabic_PP-OCRv5_mobile_rec_infer.mnn", 4043724, "6c5d0653cdcf78317321b3932b62f02814b8d3b8bd2a8760ffc7316f8727cf33", "arabic"),
            new File("ppocr_keys_arabic.txt", 2369, "7f92f7dbb9b75a4787a83bfb4f6d14a8ab515525130c9d40a9036f61cf6999e9", "arabic"),
            new File("devanagari_PP-OCRv5_mobile_rec_infer.mnn", 4000048, "8faa2c1a31b4e4bb4cedb765511edcb47d3050a493ace7abd9670250f40e15cc", "devanagari"),
            new File("ppocr_keys_devanagari.txt", 1943, "09c7440bfc5477e5c41052304b6b185aff8c4a5e8b2b4c23c1c706f6fe1ee9fc", "devanagari"),
            new File("eslav_PP-OCRv5_mobile_rec_infer.mnn", 3987604, "6d8a4d28dffc32a91d3b04e224df528adac6924c0702c955f10bc84d19cbdade", "eslav"),
            new File("ppocr_keys_eslav.txt", 1663, "3e95f1581557162870cacdba5af91a4c6be2890710d395b0c3c7578e7ee5e6eb", "eslav"),
            new File("korean_PP-OCRv5_mobile_rec_infer.mnn", 6776036, "806d9773f5f0b00c16eac2b13ba91d50de3485f28ca7669be2e7ea9a66a41e38", "korean"),
            new File("ppocr_keys_korean.txt", 47451, "a88071c68c01707489baa79ebe0405b7beb5cca229f4fc94cc3ef992328802d7", "korean"),
            new File("ta_PP-OCRv5_mobile_rec_infer.mnn", 3986628, "ae554bdd6c87139829cf30455e8e3919e3b50d8e0c24b282131ef269efc3cda4", "tamil"),
            new File("ppocr_keys_ta.txt", 1723, "85b541352ae18dc6ba6d47152d8bf8adff6b0266e605d2eef2990c1bf466117b", "tamil"),
            new File("te_PP-OCRv5_mobile_rec_infer.mnn", 3993216, "9af6053abc2ef87b99897f11bb5d1b2fa6407063ecc018e86a7fa91b486c3447", "telugu"),
            new File("ppocr_keys_te.txt", 1831, "42f83f5d3fdb50778e4fa5b66c58d99a59ab7792151c5e74f34b8ffd7b61c9d6", "telugu"),
            new File("th_PP-OCRv5_mobile_rec_infer.mnn", 3989312, "da289707e95c01dcf1e927f2512525bd1260602aa12ae677534d8f36754322ff", "thai"),
            new File("ppocr_keys_th.txt", 1767, "57f5406f94bb6688fb7077f7be65f08bbd71cecf48c01ea26c522cb5c4836b7a", "thai"),
            new File("PP-OCRv5_mobile_det_fp16.mnn", 2439100, "617b5228b101275594f96ebb6ae7662fd1618bcf8e84b0ffde1cf3b48e754951", null),
            new File("PP-OCRv5_mobile_rec_fp16.mnn", 8371960, "ff03e4204260325eabe9f4eae0ec8cc6b79b8a97a8e38a5292ba69cf02a689fc", "chinese-fp16"),
            new File("ch_PP-OCRv4_det_infer.mnn", 4736528, "857252b1789e8c1577139b53e3389473ba49c2a5b99cd33b64c51a42a2c89b99", null),
            new File("ch_PP-OCRv4_rec_infer.mnn", 10821100, "ff895dad5c9804486f5ff04964e2207e3095a6a5e5f286ff4386e2da865cd6c1", "chinese-v4"),
            new File("ppocr_keys_v4.txt", 26249, "28b2362ad4ab2dc38769aa72feb535e3a9ddb3fd2a7585a05920e6393b1dc7f7", "chinese-v4")
        };
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || revision == Revision)
                return Revision;
            throw new ArgumentException("This Paddle text profile requires revision " + Revision + ".", nameof(revision));
        }
        internal static void Prepare(string directory)
        {
            var files = Files();
            // Validate the entire pinned download before reorganizing independent recognizers.
            foreach (var file in files)
            {
                string relative = System.IO.File.Exists(Path.Combine(directory, file.Path)) ? file.Path : file.InstalledPath;
                string path = MNNPaddleTextConfig.ResolveFile(directory, relative);
                using (var stream = System.IO.File.OpenRead(path))
                using (var hash = SHA256.Create())
                    if (stream.Length != file.Bytes || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), file.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Paddle text profile file failed verification: " + file.Path);
            }
            foreach (var file in files)
            {
                if (file.Profile == null)
                    continue;
                string source = Path.Combine(directory, file.Path), target = Path.Combine(directory, file.InstalledPath);
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (System.IO.File.Exists(source))
                {
                    if (System.IO.File.Exists(target))
                        System.IO.File.Delete(target);
                    System.IO.File.Move(source, target);
                }
            }
            WriteConfig(directory, new MNNPaddleTextConfig());
            var page = new MNNPaddleOcrConfig();
            page.Validate(directory);
            System.IO.File.WriteAllText(Path.Combine(directory, MNNPaddleOcrConfig.FileName), JsonUtility.ToJson(page, true));
            WritePageConfig(directory, "mnn_paddle_ocr_v4.json", new MNNPaddleOcrConfig
            {
                graph = "ch_PP-OCRv4_det_infer.mnn", output = "sigmoid_0.tmp_0"
            });
            WritePageConfig(directory, "mnn_paddle_ocr_fp16.json", new MNNPaddleOcrConfig { graph = "PP-OCRv5_mobile_det_fp16.mnn" });
            // Both v5 precision variants use the same verified dictionary; copy only this small asset locally.
            System.IO.File.Copy(Path.Combine(directory, "recognizers", "chinese", "ppocr_keys_v5.txt"),
                Path.Combine(directory, "recognizers", "chinese-fp16", "ppocr_keys_v5.txt"), true);
            foreach (string profile in Profiles)
            {
                string graph = null, dictionary = null;
                foreach (var file in files)
                {
                    if (file.Profile != profile)
                        continue;
                    if (file.Path.EndsWith(".mnn", StringComparison.Ordinal))
                        graph = file.Path;
                    else
                        dictionary = file.Path;
                }
                if (profile == "chinese-fp16") dictionary = "ppocr_keys_v5.txt";
                WriteConfig(Path.Combine(directory, "recognizers", profile), new MNNPaddleTextConfig
                {
                    graph = graph, dictionary = dictionary, reverseArabic = profile == "arabic",
                    preferredBackend = profile == "korean" ? MNNBackendType.CPU : MNNBackendType.Auto,
                    output = profile == "chinese-v4" ? "softmax_11.tmp_0" : "fetch_name_0",
                    pageConfig = profile == "chinese-v4" ? "mnn_paddle_ocr_v4.json"
                        : profile == "chinese-fp16" ? "mnn_paddle_ocr_fp16.json" : MNNPaddleOcrConfig.FileName
                });
            }
        }

        internal static readonly string[] Profiles = { "chinese", "latin", "cyrillic", "greek", "arabic", "devanagari", "eslav", "korean", "tamil", "telugu", "thai", "chinese-v4", "chinese-fp16" };

        private static void WritePageConfig(string directory, string name, MNNPaddleOcrConfig config)
        {
            config.Validate(directory);
            System.IO.File.WriteAllText(Path.Combine(directory, name), JsonUtility.ToJson(config, true));
        }

        private static void WriteConfig(string directory, MNNPaddleTextConfig config)
        {
            config.Validate(directory);
            System.IO.File.WriteAllText(Path.Combine(directory, MNNPaddleTextConfig.FileName), JsonUtility.ToJson(config, true));
        }
    }
}
