using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNPiper
    {
        internal static string[] ReadLanguages(string directory)
        {
            var config = MNNModelData.Read(Path.Combine(directory, "config.json"));
            string path = (string)MNNModelData.Get(config, "model_path");
            if (path.EndsWith("_fp16.mnn", StringComparison.Ordinal) || path.EndsWith("_fp16_public.mnn", StringComparison.Ordinal))
                return new[]{"en"};
            var metadata = MNNModelData.Read(FindVoiceMetadata(directory, Path.GetFileNameWithoutExtension(path)));
            return new[]{MNNSpeechLanguages.Normalize((string)MNNModelData.Get(MNNModelData.Get(metadata, "espeak"), "voice"))};
        }

        internal static string[] SplitClauses(string text) => Regex.Split(text, @"(?<=[.,;:!?。，；：！？])(?![0-9])");
        internal static string[] InstalledVoices(string directory)
        {
            var config = MNNModelData.Read(Path.Combine(directory, "config.json"));
            string path = (string)MNNModelData.Get(config, "model_path");
            if (path != Path.GetFileName(path) || !path.EndsWith(".mnn", StringComparison.Ordinal))
                throw new InvalidDataException("Piper model_path must name a voice graph in the model directory.");
            bool fp16 = path.EndsWith("_fp16.mnn", StringComparison.Ordinal) || path.EndsWith("_fp16_public.mnn", StringComparison.Ordinal);
            if (!fp16)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return File.Exists(Path.Combine(directory, path)) && FindVoiceMetadata(directory, name) != null ? new[]{name} : Array.Empty<string>();
            }

            return Directory.GetFiles(directory, "*_fp16*.mnn").Select(Path.GetFileName).Where(name => name.EndsWith("_fp16.mnn", StringComparison.Ordinal) || name.EndsWith("_fp16_public.mnn", StringComparison.Ordinal)).Select(name => name.Replace("_fp16_public.mnn", "").Replace("_fp16.mnn", "")).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private string Phonemize(string text)
        {
            var info = new ProcessStartInfo{FileName = _executable, Arguments = "-q --ipa=3 -v " + Quote(_phonemeVoice) + " --path " + Quote(Path.GetDirectoryName(_data)), UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardInputEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8};
            using (var process = Process.Start(info))
            {
                process.StandardInput.WriteLine(text);
                process.StandardInput.Close();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill();
                    throw new TimeoutException("eSpeak-NG phonemization timed out.");
                }

                string output = outputTask.GetAwaiter().GetResult().Trim();
                string error = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("eSpeak-NG failed: " + error.Trim());
                return Regex.Replace(output, @"\([a-z-]+\)", "").Replace("\r", " ").Replace("\n", " ");
            }
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
