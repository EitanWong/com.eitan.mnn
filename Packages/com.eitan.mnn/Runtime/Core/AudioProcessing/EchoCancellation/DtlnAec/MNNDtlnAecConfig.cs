using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    internal sealed class MNNDtlnAecConfig
    {
        public int version = 1, units, sampleRate = 16000, blockLength = 512, hopLength = 128;
        public string repository, revision, source_revision;
        public bool gpuSplit;
        internal static MNNDtlnAecConfig Read(string directory)
        {
            var config = JsonUtility.FromJson<MNNDtlnAecConfig>(File.ReadAllText(Path.Combine(directory, MNNDtlnAecModel.ConfigName)));
            if (config == null || config.version != 1 || !MNNDtlnAecModel.IsRepository(config.repository) ||
                MNNDtlnAecModel.Units(config.repository) != config.units || config.revision != MNNDtlnAecModel.Revision(config.units) ||
                (config.gpuSplit && config.source_revision != MNNDtlnAecModel.SourceRevision) ||
                config.sampleRate != 16000 || config.blockLength != 512 || config.hopLength != 128)
                throw new InvalidDataException("Unsupported DTLN AEC profile. Install a verified 128, 256 or 512 profile with Model Manager.");
            return config;
        }
    }
}
