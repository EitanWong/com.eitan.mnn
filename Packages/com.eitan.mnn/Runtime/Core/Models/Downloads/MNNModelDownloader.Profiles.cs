using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    public sealed partial class MNNModelDownloader
    {
        public static MNNModelRepositoryFile[] DtlnAecDownloadPlan(string repository)
        {
            int units = MNNDtlnAecModel.Units(repository);
            var sizes = MNNDtlnAecModel.Sizes(units);
            var hashes = MNNDtlnAecModel.Hashes(units);
            string revision = MNNDtlnAecModel.Revision(units);
            return new[]
            {
                new MNNModelRepositoryFile {Path = MNNDtlnAecModel.Graph(units, 1), Type = "blob", Size = sizes[0], Sha256 = hashes[0], Revision = revision},
                new MNNModelRepositoryFile {Path = MNNDtlnAecModel.Graph(units, 2), Type = "blob", Size = sizes[1], Sha256 = hashes[1], Revision = revision},
                new MNNModelRepositoryFile {Path = "LICENSE", Type = "blob", Size = 1075, Sha256 = "aa95acd8c8a7341bfcdb2823694dcbb27a2a4143e860bb52db1ff0f29c85e5a1", Revision = revision}
            };
        }
        private static MNNModelRepositoryFile[] ParaformerDownloadPlan(string repository, bool modelScope)
        {
            string revision = MNNParaformerModel.ResolveRevision(null, repository, modelScope);
            return MNNParaformerModel.Paths.Select((path, i) => new MNNModelRepositoryFile{Path = path, Type = "blob", Size = MNNParaformerModel.Sizes[i], Sha256 = MNNParaformerModel.Hashes[i], Revision = revision}).Concat(new[]{new MNNModelRepositoryFile{Path = "README.md", Type = "blob", Size = modelScope ? 664 : 780, Revision = revision, Sha256 = modelScope ? "4b9f3d928a54cd1fab1fefc4f1448b0fde032577b2c12f686eedb196689856c1" : "b7e4d0cafb5b2cb578e489ee8aece93e88355cc23203e5b72cb1cc3ad92cf3df"}}).ToArray();
        }

        private static MNNModelRepositoryFile[] Qwen3AsrDownloadPlan(bool modelScope)
        {
            var files = MNNQwen3AsrModel.Paths.Select((path, i) => new MNNModelRepositoryFile{Path = path, Type = "blob", Size = MNNQwen3AsrModel.Sizes[i], Sha256 = MNNQwen3AsrModel.Hashes[i], Revision = MNNQwen3AsrModel.FileRevision(i, modelScope)});
            // Keep source notices with the inference assets.
            return files.Concat(new[]{new MNNModelRepositoryFile{Path = modelScope ? "README.md" : "LICENSE", Type = "blob", Size = modelScope ? 1456 : 11357, Sha256 = modelScope ? "722b386a136f5b4f2afe8d8ef9efc2e3db40fdf7b8db82aa9c982ac2dd8269bf" : "c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4", Revision = modelScope ? "dccf04663faa19b24af3f9d3fd0835d308d51687" : MNNQwen3AsrModel.Revision}}).ToArray();
        }

        public static MNNModelRepositoryFile[] ReferenceSpeechDownloadPlan(string repositoryId, string revision)
        {
            if (!MNNReferenceSpeechModel.IsRepository(repositoryId))
                throw new ArgumentException("Unknown speech repository.", nameof(repositoryId));
            MNNReferenceSpeechModel.ResolveRevision(repositoryId, revision);
            return MNNReferenceSpeechModel.Files(MNNReferenceSpeechModel.IsIndex(repositoryId)).Select(file => new MNNModelRepositoryFile{Path = file.Path, Type = "blob", Size = file.Bytes, Sha256 = file.Sha256, Revision = file.Revision, SourceRepository = file.Repository}).ToArray();
        }
    }
}
