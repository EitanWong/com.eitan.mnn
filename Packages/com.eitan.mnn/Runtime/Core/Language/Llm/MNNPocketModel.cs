using System;

namespace MNN.Unity
{
    /// <summary>Official Pocket instruction model identity and its pinned MNN W4A16 deployment assets.</summary>
    public static class MNNPocketModel
    {
        public const string ModelId = "MNN/MNN-Pocket-0.3B";
        public const string InstallationName = "MNN-Pocket-0.3B-MNN";
        public const string Repository = "MNN/MNN-Pocket-0.3B-MNN";
        public const string Revision = "92aaad61ae9ecc5a560402a70d1583c08f1c6442";
        public const long Bytes = 174160405;

        public static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);

        internal static string ResolveRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "master" || revision == Revision) return Revision;
            throw new ArgumentException("The Pocket deployment profile requires revision " + Revision + ".", nameof(revision));
        }

        /// <summary>Returns a fresh installation plan, including the upstream Apache 2.0 license.</summary>
        public static MNNModelRepositoryFile[] Files() => new[]
        {
            File("config.json", 502, "801311d0461dceffb4fe06f71a38b8ffb9616022626109537d3343ecc747619f"),
            File("llm_config.json", 3237, "64e1b9fc5905fc9e91a8b2007c225b70dc82ffb183ecdb4bfc28e01ac42adc45"),
            File("llm.mnn", 953112, "d42a7d9c7152b45b2b6805f9a5ab6dd95c485ad718be88ec723087d4f5d71d7c"),
            File("llm.mnn.weight", 171481330, "c9e0f2950718c057aeb2c20e5b8dd669cd02477c6c355b46d153256a312a248d"),
            File("tokenizer.mtok", 1710861, "9f806cad88e8be82e0ca3868903f3528eb1ffcbe4de1437e20549b94fccb4440"),
            File("LICENSE.txt", 11363, "c4082a47027777530d49447f8f105002466257bcb2985f5aded0736d10061729")
        };

        private static MNNModelRepositoryFile File(string path, long bytes, string hash) =>
            new MNNModelRepositoryFile { Path = path, Type = "blob", Size = bytes, Sha256 = hash, Revision = Revision };
    }
}
