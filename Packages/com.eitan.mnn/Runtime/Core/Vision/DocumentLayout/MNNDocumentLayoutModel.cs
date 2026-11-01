using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    // Pinned DETR export only. Other layout graphs need their own verified protocol.
    internal static class MNNDocumentLayoutModel
    {
        internal const string Repository = "tansen87/PP-DocLayoutV3_mnn";
        internal const string Revision = "172d63a1b5d36596dc0d665a27669ea07cb44db7";
        internal const string Graph = "PP-DocLayoutV3.mnn";
        internal const string Metadata = "layout-meta.json";
        internal static readonly string[] Files = {Graph, Metadata};
        internal static readonly long[] Sizes = {130568724, 1444};
        internal static readonly string[] Hashes = {
            "872c751527a43180e81922e72b444e9ea516545877fb9eae24433abd9e355366",
            "da1a4deb575e37038afbf855f6e69649d930c3e8bf0984100a4cf56d14c17a28"};
        internal static readonly string[] Labels = {
            "abstract", "algorithm", "aside_text", "chart", "content", "display_formula", "doc_title",
            "figure_title", "footer", "footer_image", "footnote", "formula_number", "header", "header_image",
            "image", "inline_formula", "number", "paragraph_title", "reference", "reference_content", "seal",
            "table", "text", "vertical_text", "vision_footnote"};

        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
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
                using (var stream = File.OpenRead(Path.Combine(directory, Files[i])))
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != Hashes[i])
                        throw new InvalidDataException("Document layout requires the pinned graph and matching metadata: " + Files[i]);
        }
    }
}
