using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    internal static partial class MNNHojoTtsModel
    {
        internal const string Name = "Hojo-TTS-Light-40M-V2-MNN";
        internal const string Repository = "EitanWang/" + Name;
        internal const string Revision = "ba4d52f0ea8e605f5d01c8d1243b3c288c4ac0f0";
        internal const string Config = "mnn_hojo_tts.json";
        internal static bool IsComplete(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;
            try
            {
                var config = MNNModelData.Read(Path.Combine(directory, Config));
                if (!Equals(MNNModelData.Get(config, "format"), "hojo-tts-v2-mnn") ||
                    MNNModelData.Integer(MNNModelData.Get(config, "version")) != 1 ||
                    MNNModelData.Integer(MNNModelData.Get(config, "sample_rate")) != 24000) return false;
                var voices = MNNModelData.Array(MNNModelData.Get(config, "voices"));
                if (voices.Count != 17 || voices.Cast<string>().Distinct().Count() != 17) return false;
                return Files().All(file => {
                    var path = Path.Combine(directory, file.Path);
                    return File.Exists(path) && new FileInfo(path).Length == file.Size;
                });
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidCastException)
            { return false; }
        }
    }
}
